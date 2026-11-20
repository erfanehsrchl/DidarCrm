using DidarCrm.Application;
using DidarCrm.Application.Backfill.ReleaseLegacyActivities;
using DidarCrm.Infrastructure;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Persistence;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace DidarCrm.IntegrationTests.Persistence;

public sealed class BackfillScriptTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .Build();

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        await CreateLegacySchemaAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlServer.DisposeAsync();
    }

    [Fact]
    public async Task NotificationBackfillCopiesOnlyCompletedLegacyRowsAndIsRerunnable()
    {
        var completedActivityId = Guid.NewGuid();
        var pendingActivityId = Guid.NewGuid();
        await InsertActivityAsync(
            completedActivityId,
            notificationCreated: true,
            webhookSent: false);
        await InsertActivityAsync(
            pendingActivityId,
            notificationCreated: false,
            webhookSent: false);

        var script = await ReadScriptAsync("01-backfill-notifications.sql");
        await ExecuteAsync(script);
        await ExecuteAsync(script);

        Assert.Equal(1, await CountNotificationsAsync(completedActivityId));
        Assert.Equal(0, await CountNotificationsAsync(pendingActivityId));
    }

    [Fact]
    public async Task WebhookBackfillMapsLegacyFlagsAndIsRerunnable()
    {
        var succeededActivityId = Guid.NewGuid();
        var pendingActivityId = Guid.NewGuid();
        await InsertActivityAsync(
            succeededActivityId,
            notificationCreated: false,
            webhookSent: true);
        await InsertActivityAsync(
            pendingActivityId,
            notificationCreated: false,
            webhookSent: false);

        var script = await ReadScriptAsync(
            "02-backfill-webhook-deliveries.sql");
        await ExecuteAsync(script);
        await ExecuteAsync(script);

        Assert.Equal(
            ("Succeeded", 0, 1),
            await GetWebhookStateAsync(succeededActivityId));
        Assert.Equal(
            ("Pending", 0, 1),
            await GetWebhookStateAsync(pendingActivityId));
    }

    [Fact]
    public async Task ReleaseCheckpointAndOutboxResumeWithoutDuplicateEvents()
    {
        var firstActivityId = Guid.NewGuid();
        var secondActivityId = Guid.NewGuid();
        await InsertActivityAsync(
            firstActivityId,
            notificationCreated: false,
            webhookSent: false);
        await InsertActivityAsync(
            secondActivityId,
            notificationCreated: false,
            webhookSent: false);
        await ExecuteBackfillScriptsAsync();

        await using var serviceProvider = CreateServiceProvider();

        await using (var firstScope = serviceProvider.CreateAsyncScope())
        {
            var handler = firstScope.ServiceProvider
                .GetRequiredService<ReleaseLegacyActivitiesHandler>();
            Assert.Equal(1, await handler.HandleAsync(1));
        }

        await using (var resumedScope = serviceProvider.CreateAsyncScope())
        {
            var handler = resumedScope.ServiceProvider
                .GetRequiredService<ReleaseLegacyActivitiesHandler>();
            Assert.Equal(1, await handler.HandleAsync(1));
            Assert.Equal(0, await handler.HandleAsync(1));
        }

        await using var verificationContext = CreateDbContext();
        Assert.Equal(
            2,
            await CountAllReleasesAsync());
        Assert.Equal(
            2,
            await verificationContext.Set<OutboxMessage>().CountAsync(
                message =>
                    message.Body.Contains(firstActivityId.ToString()) ||
                    message.Body.Contains(secondActivityId.ToString())));
    }

    [Fact]
    public async Task PollingPicksConcurrentLegacyWriteAndReconciliationReportsCoverage()
    {
        await using var serviceProvider = CreateServiceProvider();

        await using (var emptyScope = serviceProvider.CreateAsyncScope())
        {
            var handler = emptyScope.ServiceProvider
                .GetRequiredService<ReleaseLegacyActivitiesHandler>();
            Assert.Equal(0, await handler.HandleAsync(10));
        }

        var legacyActivityId = Guid.NewGuid();
        var currentActivityId = Guid.NewGuid();
        await InsertActivityAsync(
            legacyActivityId,
            notificationCreated: false,
            webhookSent: false);
        await InsertActivityAsync(
            currentActivityId,
            notificationCreated: false,
            webhookSent: false,
            requestKey: "current-path");

        await using (var pollingScope = serviceProvider.CreateAsyncScope())
        {
            var handler = pollingScope.ServiceProvider
                .GetRequiredService<ReleaseLegacyActivitiesHandler>();
            Assert.Equal(1, await handler.HandleAsync(10));
        }

        Assert.Equal(1, await CountReleasesAsync(legacyActivityId));
        Assert.Equal(0, await CountReleasesAsync(currentActivityId));

        var reconciliationScript = await ReadScriptAsync(
            "03-reconcile-backfill.sql");
        await ExecuteAsync(reconciliationScript);
    }

    private static async Task<string> ReadScriptAsync(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "scripts",
            "backfill",
            fileName);

        return await File.ReadAllTextAsync(path);
    }

    private async Task InsertActivityAsync(
        Guid activityId,
        bool notificationCreated,
        bool webhookSent,
        string? requestKey = null)
    {
        const string sql =
            """
            INSERT INTO [dbo].[crmActivity]
            (
                [Id], [TenantId], [ContactId], [Title], [CreatedAt],
                [WebhookSent], [NotificationCreated], [RequestKey]
            )
            VALUES
            (
                @Id, @TenantId, @ContactId, N'Historical activity',
                @CreatedAt, @WebhookSent, @NotificationCreated, @RequestKey
            );
            """;

        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", activityId);
        command.Parameters.AddWithValue("@TenantId", Guid.NewGuid());
        command.Parameters.AddWithValue("@ContactId", Guid.NewGuid());
        command.Parameters.AddWithValue(
            "@CreatedAt",
            new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
        command.Parameters.AddWithValue("@WebhookSent", webhookSent);
        command.Parameters.AddWithValue(
            "@NotificationCreated",
            notificationCreated);
        command.Parameters.AddWithValue(
            "@RequestKey",
            (object?)requestKey ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteBackfillScriptsAsync()
    {
        await ExecuteAsync(
            await ReadScriptAsync("01-backfill-notifications.sql"));
        await ExecuteAsync(
            await ReadScriptAsync("02-backfill-webhook-deliveries.sql"));
    }

    private ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(
                _sqlServer.GetConnectionString(),
                new RabbitMqOptions { Enabled = false },
                new OutboxDeliveryOptions { Enabled = false })
            .BuildServiceProvider();
    }

    private async Task<int> CountReleasesAsync(Guid activityId)
    {
        const string sql =
            """
            SELECT COUNT(*)
            FROM [dbo].[crmActivityBackfillRelease]
            WHERE [ActivityId] = @ActivityId;
            """;

        return Convert.ToInt32(
            await ExecuteScalarAsync(sql, activityId));
    }

    private async Task<int> CountAllReleasesAsync()
    {
        const string sql =
            """
            SELECT COUNT(*)
            FROM [dbo].[crmActivityBackfillRelease];
            """;

        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountNotificationsAsync(Guid activityId)
    {
        const string sql =
            """
            SELECT COUNT(*)
            FROM [dbo].[crmNotification]
            WHERE [ActivityId] = @ActivityId;
            """;

        return Convert.ToInt32(
            await ExecuteScalarAsync(sql, activityId));
    }

    private async Task<(string Status, int AttemptCount, int RowCount)>
        GetWebhookStateAsync(Guid activityId)
    {
        const string sql =
            """
            SELECT [Status], [AttemptCount],
                   COUNT(*) OVER () AS [RowCount]
            FROM [dbo].[crmWebhookDelivery]
            WHERE [ActivityId] = @ActivityId;
            """;

        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ActivityId", activityId);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return (
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2));
    }

    private async Task<object?> ExecuteScalarAsync(
        string sql,
        Guid activityId)
    {
        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ActivityId", activityId);
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(
            _sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 60
        };
        await command.ExecuteNonQueryAsync();
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
        const string script =
            """
            CREATE TABLE [dbo].[cntContact]
            (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [LastActivityAt] datetimeoffset NULL,
                CONSTRAINT [PK_cntContact] PRIMARY KEY ([Id])
            );

            CREATE TABLE [dbo].[crmActivity]
            (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [ContactId] uniqueidentifier NOT NULL,
                [Title] nvarchar(500) NOT NULL,
                [CreatedAt] datetimeoffset NOT NULL,
                [WebhookSent] bit NOT NULL
                    CONSTRAINT [DF_crmActivity_WebhookSent] DEFAULT 0,
                [NotificationCreated] bit NOT NULL
                    CONSTRAINT [DF_crmActivity_NotificationCreated] DEFAULT 0,
                CONSTRAINT [PK_crmActivity] PRIMARY KEY ([Id])
            );
            """;

        await ExecuteAsync(script);
    }

}
