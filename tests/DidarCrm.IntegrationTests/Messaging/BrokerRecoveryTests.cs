using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DidarCrm.Api.Contracts;
using DidarCrm.Api.Contracts.Activities;
using DidarCrm.Application.Activities.Events;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Domain.Notifications;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Persistence;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace DidarCrm.IntegrationTests.Messaging;

public sealed class BrokerRecoveryTests : IAsyncLifetime
{
    private const string RabbitUsername = "rabbitmq";
    private const string RabbitPassword = "rabbitmq";
    private const ushort RabbitManagementPort = 15672;

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(
        "rabbitmq:4.1-management")
        .WithUsername(RabbitUsername)
        .WithPassword(RabbitPassword)
        .WithPortBinding(RabbitManagementPort, true)
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sqlServer.StartAsync(),
            _rabbitMq.StartAsync());
        await CreateLegacySchemaAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    [Fact]
    public async Task ActivityAndOutboxPersistWhileBrokerIsDown_ThenPublishAfterRecovery()
    {
        await using var factory = new BrokerRecoveryWebApplicationFactory(
            _sqlServer.GetConnectionString(),
            _rabbitMq.Hostname,
            _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort));
        using var client = factory.CreateClient();

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        await SeedContactAsync(tenantId, contactId);

        await _rabbitMq.PauseAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/activities");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("Idempotency-Key", "broker-recovery");
        request.Content = JsonContent.Create(new
        {
            contactId,
            title = "Broker recovery"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content
            .ReadFromJsonAsync<ApiResponse<CreateActivityResponse>>();
        var activity = Assert.IsType<CreateActivityResponse>(envelope?.Data);
        Assert.True(await HasOutboxMessageAsync(activity.ActivityId));

        await _rabbitMq.UnpauseAsync();

        var deliveredAndConsumed = await WaitUntilAsync(
            async () =>
                !await HasOutboxMessageAsync(activity.ActivityId) &&
                await HasNotificationAsync(activity.ActivityId),
            TimeSpan.FromSeconds(60));

        Assert.True(
            deliveredAndConsumed,
            "The event was not delivered and consumed after RabbitMQ recovered.");
    }

    [Fact]
    public async Task RedeliveredMessageWithSameMessageIdCreatesOneNotification()
    {
        await using var factory = new BrokerRecoveryWebApplicationFactory(
            _sqlServer.GetConnectionString(),
            _rabbitMq.Hostname,
            _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort));
        using var scope = factory.Services.CreateScope();
        using var managementClient = CreateRabbitManagementClient();

        var messageId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var message = new ActivityCreatedEvent(
            messageId,
            activityId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Redelivered activity",
            DateTimeOffset.UtcNow);
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        await bus.Publish(
            message,
            context => context.MessageId = messageId);
        await bus.Publish(
            message,
            context => context.MessageId = messageId);

        var duplicateWasConsumed = await WaitUntilAsync(
            async () =>
                await CountNotificationsAsync(activityId) == 1 &&
                await CountInboxEntriesAsync(messageId) == 1 &&
                await GetNotificationQueueMessageCountAsync(managementClient) == 0,
            TimeSpan.FromSeconds(60));

        var notificationCount = await CountNotificationsAsync(activityId);
        var matchingInboxCount = await CountInboxEntriesAsync(messageId);
        var totalInboxCount = await CountInboxEntriesAsync();
        var queueMessageCount =
            await GetNotificationQueueMessageCountAsync(managementClient);

        Assert.True(
            duplicateWasConsumed,
            "The duplicate delivery was not consumed within the expected time. " +
            $"Notifications: {notificationCount}; " +
            $"Matching inbox entries: {matchingInboxCount}; " +
            $"Total inbox entries: {totalInboxCount}; " +
            $"Queue messages: {queueMessageCount?.ToString() ?? "unavailable"}.");
        Assert.Equal(1, notificationCount);
        Assert.Equal(1, matchingInboxCount);
    }

    private async Task SeedContactAsync(Guid tenantId, Guid contactId)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Contacts.Add(
            Contact.Create(
                ContactId.From(contactId),
                TenantId.From(tenantId)));
        await dbContext.SaveChangesAsync();
    }

    private async Task<bool> HasOutboxMessageAsync(Guid activityId)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.Set<OutboxMessage>().AnyAsync(message =>
            message.Body.Contains(activityId.ToString()));
    }

    private async Task<bool> HasNotificationAsync(Guid activityId)
    {
        await using var dbContext = CreateDbContext();
        var id = ActivityId.From(activityId);

        return await dbContext.Set<ActivityNotification>().AnyAsync(
            notification => notification.ActivityId == id);
    }

    private async Task<int> CountNotificationsAsync(Guid activityId)
    {
        await using var dbContext = CreateDbContext();
        var id = ActivityId.From(activityId);

        return await dbContext.Set<ActivityNotification>().CountAsync(
            notification => notification.ActivityId == id);
    }

    private async Task<int> CountInboxEntriesAsync(Guid messageId)
    {
        await using var dbContext = CreateDbContext();

        return await dbContext.Set<InboxState>().CountAsync(
            inbox => inbox.MessageId == messageId);
    }

    private async Task<int> CountInboxEntriesAsync()
    {
        await using var dbContext = CreateDbContext();

        return await dbContext.Set<InboxState>().CountAsync();
    }

    private HttpClient CreateRabbitManagementClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(
                $"http://{_rabbitMq.Hostname}:" +
                $"{_rabbitMq.GetMappedPublicPort(RabbitManagementPort)}/")
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        $"{RabbitUsername}:{RabbitPassword}")));

        return client;
    }

    private static async Task<int?> GetNotificationQueueMessageCountAsync(
        HttpClient client)
    {
        using var response = await client.GetAsync(
            $"api/queues/%2F/{MessagingTopology.NotificationQueue}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var content = await response.Content.ReadAsStreamAsync();
        using var queue = await JsonDocument.ParseAsync(content);

        return queue.RootElement.TryGetProperty("messages", out var messages)
            ? messages.GetInt32()
            : null;
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

    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        using var timeoutCancellation = new CancellationTokenSource(timeout);

        while (!timeoutCancellation.IsCancellationRequested)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                timeoutCancellation.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return false;
    }

    private sealed class BrokerRecoveryWebApplicationFactory(
        string connectionString,
        string rabbitHost,
        ushort rabbitPort) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Database", connectionString);
            builder.UseSetting("RabbitMq:Enabled", "true");
            builder.UseSetting("RabbitMq:Host", rabbitHost);
            builder.UseSetting("RabbitMq:Port", rabbitPort.ToString());
            builder.UseSetting("RabbitMq:Username", RabbitUsername);
            builder.UseSetting("RabbitMq:Password", RabbitPassword);
            builder.UseSetting("OutboxDelivery:Enabled", "true");
            builder.UseSetting("OutboxDelivery:QueryDelay", "00:00:00.200");

            builder.ConfigureServices(services =>
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = TimeSpan.FromSeconds(30);
                }));
        }
    }
}
