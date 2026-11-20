using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Activities.CreateActivity;
using DidarCrm.Application.Activities.Events;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Infrastructure;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Persistence;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task ConcurrentRequests_KeepMaximumLastActivityAtAndPersistBothActivities()
    {
        var tenantId = TenantId.From(Guid.NewGuid());
        var contactId = ContactId.From(Guid.NewGuid());
        var originalLastActivityAt = new DateTimeOffset(
            2026,
            11,
            20,
            8,
            0,
            0,
            TimeSpan.Zero);
        var earlierActivityAt = originalLastActivityAt.AddMinutes(5);
        var laterActivityAt = originalLastActivityAt.AddMinutes(10);

        await SeedContactAsync(tenantId, contactId, originalLastActivityAt);

        await using var serviceProvider = new ServiceCollection()
            .AddInfrastructure(_sqlServer.GetConnectionString())
            .BuildServiceProvider();
        await using var earlierScope = serviceProvider.CreateAsyncScope();
        await using var laterScope = serviceProvider.CreateAsyncScope();

        var eventPublisher = new CoordinatedEventPublisher();
        var laterSaveCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var earlierUnitOfWork = new OrderedUnitOfWork(
            earlierScope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            laterSaveCompleted.Task);
        var laterUnitOfWork = new OrderedUnitOfWork(
            laterScope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            Task.CompletedTask,
            laterSaveCompleted);
        var earlierHandler = CreateHandler(
            earlierScope.ServiceProvider,
            eventPublisher,
            earlierUnitOfWork,
            earlierActivityAt);
        var laterHandler = CreateHandler(
            laterScope.ServiceProvider,
            eventPublisher,
            laterUnitOfWork,
            laterActivityAt);

        var results = await Task.WhenAll(
            earlierHandler.HandleAsync(
                new CreateActivityCommand(
                    tenantId.Value,
                    contactId.Value,
                    "concurrent-earlier",
                    "Earlier activity")),
            laterHandler.HandleAsync(
                new CreateActivityCommand(
                    tenantId.Value,
                    contactId.Value,
                    "concurrent-later",
                    "Later activity")));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, earlierUnitOfWork.SaveCount);
        Assert.Equal(1, laterUnitOfWork.SaveCount);

        await using var verificationContext = CreateDbContext();
        var persistedContact = await verificationContext.Contacts
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(laterActivityAt, persistedContact.LastActivityAt);
        Assert.Equal(2, await verificationContext.Activities.CountAsync());
    }

    [Fact]
    public async Task CreateActivity_PersistsEventInMassTransitBusOutbox()
    {
        var tenantId = TenantId.From(Guid.NewGuid());
        var contactId = ContactId.From(Guid.NewGuid());
        var createdAtUtc = new DateTimeOffset(
            2026,
            11,
            20,
            12,
            0,
            0,
            TimeSpan.Zero);

        await SeedContactAsync(tenantId, contactId, createdAtUtc.AddMinutes(-1));

        await using var serviceProvider = new ServiceCollection()
            .AddInfrastructure(
                _sqlServer.GetConnectionString(),
                new RabbitMqOptions { Enabled = false })
            .BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var handler = new CreateActivityHandler(
            scope.ServiceProvider.GetRequiredService<IActivityRepository>(),
            scope.ServiceProvider.GetRequiredService<IContactRepository>(),
            scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            new FixedClock(createdAtUtc));

        var result = await handler.HandleAsync(
            new CreateActivityCommand(
                tenantId.Value,
                contactId.Value,
                "bus-outbox-request",
                "Bus outbox activity"));

        Assert.True(result.IsSuccess);

        await using var verificationContext = CreateDbContext();
        var outboxMessage = await verificationContext
            .Set<OutboxMessage>()
            .AsNoTracking()
            .SingleAsync(message =>
                message.Body.Contains(
                    result.Value.ActivityId.ToString()));

        Assert.Contains(
            nameof(ActivityCreatedEvent),
            outboxMessage.MessageType,
            StringComparison.Ordinal);
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

    private async Task SeedContactAsync(
        TenantId tenantId,
        ContactId contactId,
        DateTimeOffset lastActivityAt)
    {
        await using var dbContext = CreateDbContext();

        dbContext.Contacts.Add(
            Contact.Create(contactId, tenantId, lastActivityAt));

        await dbContext.SaveChangesAsync();
    }

    private static CreateActivityHandler CreateHandler(
        IServiceProvider serviceProvider,
        IIntegrationEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        DateTimeOffset utcNow)
    {
        return new CreateActivityHandler(
            serviceProvider.GetRequiredService<IActivityRepository>(),
            serviceProvider.GetRequiredService<IContactRepository>(),
            eventPublisher,
            unitOfWork,
            new FixedClock(utcNow));
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

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class CoordinatedEventPublisher : IIntegrationEventPublisher
    {
        private readonly TaskCompletionSource _bothRequestsAreReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _publishCount;

        public async Task PublishAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _publishCount) == 2)
            {
                _bothRequestsAreReady.TrySetResult();
            }

            await _bothRequestsAreReady.Task.WaitAsync(
                TimeSpan.FromSeconds(30),
                cancellationToken);
        }
    }

    private sealed class OrderedUnitOfWork(
        IUnitOfWork innerUnitOfWork,
        Task waitBeforeSave,
        TaskCompletionSource? saveCompleted = null) : IUnitOfWork
    {
        private int _saveCount;

        public int SaveCount => Volatile.Read(ref _saveCount);

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _saveCount);

            await waitBeforeSave.WaitAsync(
                TimeSpan.FromSeconds(30),
                cancellationToken);

            try
            {
                await innerUnitOfWork.SaveChangesAsync(cancellationToken);
                saveCompleted?.TrySetResult();
            }
            catch (Exception exception)
            {
                saveCompleted?.TrySetException(exception);
                throw;
            }
        }
    }
}
