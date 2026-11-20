using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Activities.Events;

namespace DidarCrm.Application.Backfill.ReleaseLegacyActivities;

public sealed class ReleaseLegacyActivitiesHandler(
    ILegacyActivityBackfillRepository repository,
    IIntegrationEventPublisher eventPublisher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<int> HandleAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var activities = await repository.GetUnreleasedBatchAsync(
            batchSize,
            cancellationToken);

        foreach (var activity in activities)
        {
            var messageId = Guid.NewGuid();
            await eventPublisher.PublishAsync(
                new ActivityCreatedEvent(
                    messageId,
                    activity.ActivityId,
                    activity.TenantId,
                    activity.ContactId,
                    activity.Title,
                    activity.CreatedAtUtc),
                cancellationToken);
            repository.AddRelease(
                activity.ActivityId,
                messageId,
                clock.UtcNow);
        }

        if (activities.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return activities.Count;
    }
}
