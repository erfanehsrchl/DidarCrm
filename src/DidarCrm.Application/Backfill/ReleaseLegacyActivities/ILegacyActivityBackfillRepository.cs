namespace DidarCrm.Application.Backfill.ReleaseLegacyActivities;

public interface ILegacyActivityBackfillRepository
{
    Task<IReadOnlyList<LegacyActivityCandidate>> GetUnreleasedBatchAsync(
        int batchSize,
        CancellationToken cancellationToken);

    void AddRelease(
        Guid activityId,
        Guid messageId,
        DateTimeOffset releasedAtUtc);
}
