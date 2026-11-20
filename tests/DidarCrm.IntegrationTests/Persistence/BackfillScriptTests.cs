using DidarCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
        bool webhookSent)
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
                @CreatedAt, @WebhookSent, @NotificationCreated, NULL
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
        await command.ExecuteNonQueryAsync();
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
