namespace DidarCrm.Application.Backfill.ReleaseLegacyActivities;

public sealed record LegacyActivityCandidate(
    Guid ActivityId,
    Guid TenantId,
    Guid ContactId,
    string Title,
    DateTimeOffset CreatedAtUtc);
