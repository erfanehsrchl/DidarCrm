using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Activities.CreateActivity;
using DidarCrm.Application.Activities.Events;
using DidarCrm.Application.Common.Results;
using DidarCrm.Domain.Activities;

namespace DidarCrm.UnitTests.Application.Activities;

public sealed class CreateActivityHandlerTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 11, 19, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_creates_activity_updates_contact_and_publishes_event()
    {
        var activityRepository = new ActivityRepositoryFake();
        var contactRepository = new ContactRepositoryFake();
        var eventPublisher = new EventPublisherFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = CreateHandler(
            activityRepository,
            contactRepository,
            eventPublisher,
            unitOfWork);
        var command = new CreateActivityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-123",
            " Follow up ");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(CreatedAtUtc, result.Value.CreatedAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCount);

        var activity = Assert.Single(activityRepository.AddedActivities);
        Assert.Equal(result.Value.ActivityId, activity.Id.Value);
        Assert.Equal(command.TenantId, activity.TenantId.Value);
        Assert.Equal(command.ContactId, activity.ContactId.Value);
        Assert.Equal(command.RequestKey, activity.RequestKey.Value);
        Assert.Equal("Follow up", activity.Title.Value);

        var contactUpdate = Assert.Single(contactRepository.Updates);
        Assert.Equal(command.TenantId, contactUpdate.TenantId.Value);
        Assert.Equal(command.ContactId, contactUpdate.ContactId.Value);
        Assert.Equal(CreatedAtUtc, contactUpdate.CreatedAtUtc);

        var activityCreated = Assert.IsType<ActivityCreatedEvent>(
            Assert.Single(eventPublisher.PublishedEvents));
        Assert.NotEqual(Guid.Empty, activityCreated.MessageId);
        Assert.Equal(activity.Id.Value, activityCreated.ActivityId);
        Assert.Equal(command.TenantId, activityCreated.TenantId);
        Assert.Equal(command.ContactId, activityCreated.ContactId);
        Assert.Equal("Follow up", activityCreated.Title);
        Assert.Equal(CreatedAtUtc, activityCreated.OccurredAtUtc);
    }

    [Fact]
    public async Task Handle_returns_validation_failure_for_invalid_command()
    {
        var unitOfWork = new UnitOfWorkFake();
        var handler = CreateHandler(unitOfWork: unitOfWork);
        var command = new CreateActivityCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "request-123",
            "Follow up");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Activity.Validation", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_contact_does_not_exist()
    {
        var activityRepository = new ActivityRepositoryFake();
        var contactRepository = new ContactRepositoryFake { ContactExists = false };
        var eventPublisher = new EventPublisherFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = CreateHandler(
            activityRepository,
            contactRepository,
            eventPublisher,
            unitOfWork);
        var command = new CreateActivityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-123",
            "Follow up");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Contact.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Empty(activityRepository.AddedActivities);
        Assert.Empty(eventPublisher.PublishedEvents);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_returns_existing_activity_for_same_idempotency_key_and_payload()
    {
        var command = new CreateActivityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "request-123",
            " Follow up ");
        var existingActivity = CustomerActivity.Create(
            TenantId.From(command.TenantId),
            ContactId.From(command.ContactId),
            RequestKey.Create(command.RequestKey),
            ActivityTitle.Create("Follow up"),
            CreatedAtUtc.AddMinutes(-5));
        var activityRepository = new ActivityRepositoryFake
        {
            ExistingActivity = existingActivity
        };
        var contactRepository = new ContactRepositoryFake();
        var eventPublisher = new EventPublisherFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = CreateHandler(
            activityRepository,
            contactRepository,
            eventPublisher,
            unitOfWork);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingActivity.Id.Value, result.Value.ActivityId);
        Assert.Equal(existingActivity.CreatedAtUtc, result.Value.CreatedAtUtc);
        Assert.Empty(activityRepository.AddedActivities);
        Assert.Empty(contactRepository.Updates);
        Assert.Empty(eventPublisher.PublishedEvents);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_returns_conflict_when_idempotency_key_has_different_payload()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var activityRepository = new ActivityRepositoryFake
        {
            ExistingActivity = CustomerActivity.Create(
                TenantId.From(tenantId),
                ContactId.From(contactId),
                RequestKey.Create("request-123"),
                ActivityTitle.Create("Original title"),
                CreatedAtUtc.AddMinutes(-5))
        };
        var contactRepository = new ContactRepositoryFake();
        var eventPublisher = new EventPublisherFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = CreateHandler(
            activityRepository,
            contactRepository,
            eventPublisher,
            unitOfWork);
        var command = new CreateActivityCommand(
            tenantId,
            contactId,
            "request-123",
            "Different title");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Activity.IdempotencyConflict", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Empty(activityRepository.AddedActivities);
        Assert.Empty(contactRepository.Updates);
        Assert.Empty(eventPublisher.PublishedEvents);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    private static CreateActivityHandler CreateHandler(
        ActivityRepositoryFake? activityRepository = null,
        ContactRepositoryFake? contactRepository = null,
        EventPublisherFake? eventPublisher = null,
        UnitOfWorkFake? unitOfWork = null)
    {
        return new CreateActivityHandler(
            activityRepository ?? new ActivityRepositoryFake(),
            contactRepository ?? new ContactRepositoryFake(),
            eventPublisher ?? new EventPublisherFake(),
            unitOfWork ?? new UnitOfWorkFake(),
            new ClockFake(CreatedAtUtc));
    }

    private sealed class ActivityRepositoryFake : IActivityRepository
    {
        public CustomerActivity? ExistingActivity { get; init; }

        public List<CustomerActivity> AddedActivities { get; } = [];

        public Task<CustomerActivity?> GetByRequestKeyAsync(
            TenantId tenantId,
            RequestKey requestKey,
            CancellationToken cancellationToken) => Task.FromResult(ExistingActivity);

        public Task AddAsync(CustomerActivity activity, CancellationToken cancellationToken)
        {
            AddedActivities.Add(activity);
            return Task.CompletedTask;
        }
    }

    private sealed class ContactRepositoryFake : IContactRepository
    {
        public bool ContactExists { get; init; } = true;

        public List<(TenantId TenantId, ContactId ContactId, DateTimeOffset CreatedAtUtc)> Updates { get; } = [];

        public Task<bool> AdvanceLastActivityAtAsync(
            TenantId tenantId,
            ContactId contactId,
            DateTimeOffset activityCreatedAtUtc,
            CancellationToken cancellationToken)
        {
            Updates.Add((tenantId, contactId, activityCreatedAtUtc));
            return Task.FromResult(ContactExists);
        }
    }

    private sealed class EventPublisherFake : IIntegrationEventPublisher
    {
        public List<IIntegrationEvent> PublishedEvents { get; } = [];

        public Task PublishAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            PublishedEvents.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ClockFake(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
