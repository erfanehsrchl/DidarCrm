using DidarCrm.Application.Backfill.ReleaseLegacyActivities;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;
using DidarCrm.Infrastructure.Backfill;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence.Repositories;

internal sealed class LegacyActivityBackfillRepository(
    CrmDbContext dbContext)
    : ILegacyActivityBackfillRepository
{
    public async Task<IReadOnlyList<LegacyActivityCandidate>>
        GetUnreleasedBatchAsync(
            int batchSize,
            CancellationToken cancellationToken)
    {
        var releases = dbContext.Set<LegacyActivityRelease>();
        var activities = await dbContext.Activities
            .AsNoTracking()
            .Where(activity => activity.RequestKey == null!)
            .Where(activity => !releases.Any(release =>
                release.ActivityId == activity.Id))
            .Where(activity =>
                !dbContext.Notifications.Any(notification =>
                    notification.ActivityId == activity.Id) ||
                !dbContext.WebhookDeliveries.Any(delivery =>
                    delivery.ActivityId == activity.Id &&
                    delivery.Status == WebhookDeliveryStatus.Succeeded))
            .OrderBy(activity => activity.CreatedAtUtc)
            .ThenBy(activity => activity.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return activities
            .Select(activity => new LegacyActivityCandidate(
                activity.Id.Value,
                activity.TenantId.Value,
                activity.ContactId.Value,
                activity.Title.Value,
                activity.CreatedAtUtc))
            .ToList();
    }

    public void AddRelease(
        Guid activityId,
        Guid messageId,
        DateTimeOffset releasedAtUtc)
    {
        dbContext.Set<LegacyActivityRelease>().Add(
            LegacyActivityRelease.Create(
                ActivityId.From(activityId),
                messageId,
                releasedAtUtc));
    }
}
