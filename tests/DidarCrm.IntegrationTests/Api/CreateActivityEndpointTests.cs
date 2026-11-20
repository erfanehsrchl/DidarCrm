using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DidarCrm.Api.Contracts;
using DidarCrm.Api.Contracts.Activities;
using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace DidarCrm.IntegrationTests.Api;

public sealed class CreateActivityEndpointTests(
    CreateActivityApiFixture fixture)
    : IClassFixture<CreateActivityApiFixture>
{
    [Fact]
    public async Task Liveness_ReturnsOkWithoutCheckingDependencies()
    {
        using var response = await fixture.Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_ReturnsOkWhenDatabaseAndBusAreReady()
    {
        using var response = await fixture.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ReturnsSameOkResponseForIdempotentReplay()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        const string requestKey = "endpoint-replay";

        await fixture.SeedContactAsync(tenantId, contactId);

        using var firstResponse = await fixture.PostActivityAsync(
            tenantId,
            contactId,
            requestKey,
            "Follow up");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstEnvelope = await firstResponse.Content
            .ReadFromJsonAsync<ApiResponse<CreateActivityResponse>>();

        Assert.NotNull(firstEnvelope);
        Assert.True(firstEnvelope.IsSuccess);
        Assert.Null(firstEnvelope.Message);
        var firstBody = Assert.IsType<CreateActivityResponse>(
            firstEnvelope.Data);
        Assert.NotEqual(Guid.Empty, firstBody.ActivityId);
        Assert.Equal(CreateActivityApiFixture.UtcNow, firstBody.CreatedAtUtc);
        Assert.Null(firstResponse.Headers.Location);

        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("replay", firstJson, StringComparison.OrdinalIgnoreCase);

        using var replayResponse = await fixture.PostActivityAsync(
            tenantId,
            contactId,
            requestKey,
            "Follow up");

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var replayEnvelope = await replayResponse.Content
            .ReadFromJsonAsync<ApiResponse<CreateActivityResponse>>();

        Assert.NotNull(replayEnvelope);
        Assert.True(replayEnvelope.IsSuccess);
        Assert.Equal(firstBody, replayEnvelope.Data);
    }

    [Fact]
    public async Task Post_ReturnsPersianValidationErrorsForHeadersAndBody()
    {
        using var bodyRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/activities");
        bodyRequest.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        bodyRequest.Headers.Add("Idempotency-Key", "validation-test");
        bodyRequest.Content = JsonContent.Create(new
        {
            contactId = Guid.Empty,
            title = " "
        });

        using var bodyResponse = await fixture.Client.SendAsync(bodyRequest);

        Assert.Equal(HttpStatusCode.BadRequest, bodyResponse.StatusCode);

        var bodyEnvelope = await bodyResponse.Content
            .ReadFromJsonAsync<ApiResponse<object>>();

        Assert.NotNull(bodyEnvelope);
        Assert.False(bodyEnvelope.IsSuccess);
        Assert.Equal("Request.Validation", bodyEnvelope.Code);
        Assert.Contains("شناسه مخاطب الزامی است.", bodyEnvelope.Message);
        Assert.Contains("عنوان فعالیت الزامی است.", bodyEnvelope.Message);

        using var headersResponse = await fixture.Client.PostAsJsonAsync(
            "/api/v1/activities",
            new
            {
                contactId = Guid.NewGuid(),
                title = "Follow up"
            });

        Assert.Equal(HttpStatusCode.BadRequest, headersResponse.StatusCode);

        var headersEnvelope = await headersResponse.Content
            .ReadFromJsonAsync<ApiResponse<object>>();

        Assert.NotNull(headersEnvelope);
        Assert.False(headersEnvelope.IsSuccess);
        Assert.Equal("Request.Validation", headersEnvelope.Code);
        Assert.Equal(
            "مقادیر ارسال‌شده معتبر نیستند.",
            headersEnvelope.Message);
    }

    [Fact]
    public async Task Post_MapsNotFoundAndIdempotencyConflictToApiResponse()
    {
        var tenantId = Guid.NewGuid();
        var missingContactId = Guid.NewGuid();

        using var notFoundResponse = await fixture.PostActivityAsync(
            tenantId,
            missingContactId,
            "not-found",
            "Follow up");

        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
        await AssertErrorCodeAsync(notFoundResponse, "Contact.NotFound");

        var contactId = Guid.NewGuid();
        await fixture.SeedContactAsync(tenantId, contactId);

        using var successResponse = await fixture.PostActivityAsync(
            tenantId,
            contactId,
            "conflicting-request",
            "Original title");
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        using var conflictResponse = await fixture.PostActivityAsync(
            tenantId,
            contactId,
            "conflicting-request",
            "Different title");

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        await AssertErrorCodeAsync(
            conflictResponse,
            "Activity.IdempotencyConflict");
    }

    [Fact]
    public async Task Swagger_DescribesControllerResponsesAndHeaders()
    {
        using var response = await fixture.Client.GetAsync(
            "/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(contentStream);

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/activities")
            .GetProperty("post");
        var responses = operation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.True(responses.TryGetProperty("409", out _));

        var parameters = operation
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("X-Tenant-Id", parameters);
        Assert.Contains("Idempotency-Key", parameters);
    }

    private static async Task AssertErrorCodeAsync(
        HttpResponseMessage response,
        string expectedCode)
    {
        var envelope = await response.Content
            .ReadFromJsonAsync<ApiResponse<object>>();

        Assert.NotNull(envelope);
        Assert.False(envelope.IsSuccess);
        Assert.Equal(expectedCode, envelope.Code);
    }
}

public sealed class HealthEndpointFailureTests
{
    [Fact]
    public async Task UnavailableDatabase_AffectsReadinessButNotLiveness()
    {
        await using var factory = new UnavailableDatabaseWebApplicationFactory();
        using var client = factory.CreateClient();

        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            readyResponse.StatusCode);
    }

    private sealed class UnavailableDatabaseWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RabbitMq:Enabled", "false");
            builder.UseSetting(
                "ConnectionStrings:Database",
                "Server=127.0.0.1,1;Database=unavailable;" +
                "User Id=unused;Password=unused;Connect Timeout=1;" +
                "TrustServerCertificate=True");
        }
    }
}

public sealed class CreateActivityApiFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset UtcNow = new(
        2026,
        11,
        20,
        12,
        0,
        0,
        TimeSpan.Zero);

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .Build();
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        await CreateLegacySchemaAsync();

        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        _factory = new CreateActivityWebApplicationFactory(
            _sqlServer.GetConnectionString());
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _sqlServer.DisposeAsync();
    }

    public async Task SeedContactAsync(Guid tenantId, Guid contactId)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Contacts.Add(
            Contact.Create(
                ContactId.From(contactId),
                TenantId.From(tenantId)));
        await dbContext.SaveChangesAsync();
    }

    public async Task<HttpResponseMessage> PostActivityAsync(
        Guid tenantId,
        Guid contactId,
        string requestKey,
        string title)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/activities");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("Idempotency-Key", requestKey);
        request.Content = JsonContent.Create(new { contactId, title });

        return await Client.SendAsync(request);
    }

    private CrmDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString())
            .Options;

        return new CrmDbContext(options);
    }

    private async Task CreateLegacySchemaAsync()
    {
        const string sql =
            """
            CREATE TABLE [cntContact] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [LastActivityAt] datetimeoffset NULL,
                CONSTRAINT [PK_cntContact] PRIMARY KEY ([Id])
            );

            CREATE TABLE [crmActivity] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [ContactId] uniqueidentifier NOT NULL,
                [Title] nvarchar(500) NOT NULL,
                [CreatedAt] datetimeoffset NOT NULL,
                [WebhookSent] bit NOT NULL CONSTRAINT [DF_crmActivity_WebhookSent] DEFAULT 0,
                [NotificationCreated] bit NOT NULL CONSTRAINT [DF_crmActivity_NotificationCreated] DEFAULT 0,
                CONSTRAINT [PK_crmActivity] PRIMARY KEY ([Id])
            );
            """;

        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class CreateActivityWebApplicationFactory(
        string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RabbitMq:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CrmDbContext>>();
                services.RemoveAll<CrmDbContext>();
                services.AddDbContext<CrmDbContext>(options =>
                    options.UseSqlServer(connectionString));

                services.RemoveAll<IIntegrationEventPublisher>();
                services.AddSingleton<IIntegrationEventPublisher,
                    NoOpIntegrationEventPublisher>();

                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(UtcNow));
            });
        }
    }

    private sealed class NoOpIntegrationEventPublisher
        : IIntegrationEventPublisher
    {
        public Task PublishAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
