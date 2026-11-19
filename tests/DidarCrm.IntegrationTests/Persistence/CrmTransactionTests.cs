using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Infrastructure.Persistence;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DidarCrm.IntegrationTests.Persistence;

public sealed class CrmTransactionTests : IAsyncLifetime
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

    public Task DisposeAsync()
    {
        return _sqlServer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task SaveChanges_WhenRequestKeyConflicts_RollsBackAllTrackedChanges()
    {
        var tenantId = TenantId.From(Guid.NewGuid());
        var contactId = ContactId.From(Guid.NewGuid());
        var requestKey = RequestKey.Create("rollback-request");
        var originalLastActivityAt = new DateTimeOffset(
            2026,
            11,
            20,
            8,
            0,
            0,
            TimeSpan.Zero);
        var attemptedLastActivityAt = originalLastActivityAt.AddMinutes(5);

        await SeedExistingRequestAsync(
            tenantId,
            contactId,
            requestKey,
            originalLastActivityAt);

        await using (var dbContext = CreateDbContext())
        {
            var contact = await dbContext.Contacts.SingleAsync();
            contact.AdvanceLastActivityAt(attemptedLastActivityAt);

            dbContext.Activities.Add(
                CustomerActivity.Create(
                    tenantId,
                    contactId,
                    requestKey,
                    ActivityTitle.Create("Conflicting activity"),
                    attemptedLastActivityAt));

            dbContext.Set<OutboxMessage>().Add(
                new OutboxMessage
                {
                    MessageId = Guid.NewGuid(),
                    SentTime = attemptedLastActivityAt.UtcDateTime,
                    ContentType = "application/json",
                    MessageType = "urn:message:DidarCrm:ActivityCreatedEvent",
                    Body = "{}"
                });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => dbContext.SaveChangesAsync());
        }

        await using (var verificationContext = CreateDbContext())
        {
            var persistedContact = await verificationContext.Contacts
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(originalLastActivityAt, persistedContact.LastActivityAt);
            Assert.Equal(1, await verificationContext.Activities.CountAsync());
            Assert.Equal(
                0,
                await verificationContext.Set<OutboxMessage>().CountAsync());
        }
    }

    private async Task SeedExistingRequestAsync(
        TenantId tenantId,
        ContactId contactId,
        RequestKey requestKey,
        DateTimeOffset createdAtUtc)
    {
        await using var dbContext = CreateDbContext();

        dbContext.Contacts.Add(
            Contact.Create(contactId, tenantId, createdAtUtc));
        dbContext.Activities.Add(
            CustomerActivity.Create(
                tenantId,
                contactId,
                requestKey,
                ActivityTitle.Create("Existing activity"),
                createdAtUtc));

        await dbContext.SaveChangesAsync();
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
}
