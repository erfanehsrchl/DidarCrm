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
        ArgumentNullException.ThrowIfNull(command);

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

        var createdAtUtc = _clock.UtcNow;
        var contactExists = await _contactRepository.AdvanceLastActivityAtAsync(
            tenantId,
            contactId,
            createdAtUtc,
            cancellationToken);

        if (!contactExists)
        {
            return Result<CreateActivityResponse>.Failure(
                new Error(
                    "Contact.NotFound",
                    "The contact was not found for the specified tenant.",
                    ErrorType.NotFound));
        }

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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateActivityResponse>.Success(
            new CreateActivityResponse(activity.Id.Value, activity.CreatedAtUtc));
    }
}
