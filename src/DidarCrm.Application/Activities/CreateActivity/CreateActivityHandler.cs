using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Activities.Events;
using DidarCrm.Application.Common.Results;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Common;

namespace DidarCrm.Application.Activities.CreateActivity;

public sealed class CreateActivityHandler
{
    private readonly IActivityRepository _activityRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateActivityHandler(
        IActivityRepository activityRepository,
        IContactRepository contactRepository,
        IIntegrationEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _activityRepository = activityRepository;
        _contactRepository = contactRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreateActivityResponse>> HandleAsync(
        CreateActivityCommand command,
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId;
        ContactId contactId;
        RequestKey requestKey;
        ActivityTitle title;

        try
        {
            tenantId = TenantId.From(command.TenantId);
            contactId = ContactId.From(command.ContactId);
            requestKey = RequestKey.Create(command.RequestKey);
            title = ActivityTitle.Create(command.Title);
        }
        catch (DomainValidationException exception)
        {
            return Result<CreateActivityResponse>.Failure(
                new Error("Activity.Validation", exception.Message, ErrorType.Validation));
        }

        var existingActivity = await _activityRepository.GetByRequestKeyAsync(
            tenantId,
            requestKey,
            cancellationToken);

        if (existingActivity is not null)
        {
            return CreateResultForExistingActivity(existingActivity, contactId, title);
        }

        var createdAtUtc = _clock.UtcNow;
        var contact = await _contactRepository.GetAsync(
            tenantId,
            contactId,
            cancellationToken);

        if (contact is null)
        {
            return Result<CreateActivityResponse>.Failure(
                new Error(
                    "Contact.NotFound",
                    "The contact was not found for the specified tenant.",
                    ErrorType.NotFound));
        }

        contact.AdvanceLastActivityAt(createdAtUtc);

        var activity = CustomerActivity.Create(
            tenantId,
            contactId,
            requestKey,
            title,
            createdAtUtc);

        await _activityRepository.AddAsync(activity, cancellationToken);

        await _eventPublisher.PublishAsync(
            new ActivityCreatedEvent(
                Guid.NewGuid(),
                activity.Id.Value,
                activity.TenantId.Value,
                activity.ContactId.Value,
                activity.Title.Value,
                activity.CreatedAtUtc),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRequestKeyException)
        {
            existingActivity = await _activityRepository.GetByRequestKeyAsync(
                tenantId,
                requestKey,
                cancellationToken);

            if (existingActivity is null)
            {
                throw;
            }

            return CreateResultForExistingActivity(existingActivity, contactId, title);
        }

        return Result<CreateActivityResponse>.Success(
            new CreateActivityResponse(activity.Id.Value, activity.CreatedAtUtc));
    }

    private static Result<CreateActivityResponse> CreateResultForExistingActivity(
        CustomerActivity existingActivity,
        ContactId contactId,
        ActivityTitle title)
    {
        if (existingActivity.ContactId != contactId || existingActivity.Title != title)
        {
            return Result<CreateActivityResponse>.Failure(
                new Error(
                    "Activity.IdempotencyConflict",
                    "The idempotency key has already been used with a different payload.",
                    ErrorType.Conflict));
        }

        return Result<CreateActivityResponse>.Success(
            new CreateActivityResponse(
                existingActivity.Id.Value,
                existingActivity.CreatedAtUtc));
    }
}
