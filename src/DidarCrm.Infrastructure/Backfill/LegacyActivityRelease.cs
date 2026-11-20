using DidarCrm.Domain.Activities;

namespace DidarCrm.Infrastructure.Backfill;

internal sealed class LegacyActivityRelease
{
    private LegacyActivityRelease()
    {
    }

    private LegacyActivityRelease(
        ActivityId activityId,
        Guid messageId,
        DateTimeOffset releasedAtUtc)
    {
        ActivityId = activityId;
        MessageId = messageId;
        ReleasedAtUtc = releasedAtUtc;
    }

    public ActivityId ActivityId { get; private set; } = null!;

    public Guid MessageId { get; private set; }

    public DateTimeOffset ReleasedAtUtc { get; private set; }

    public static LegacyActivityRelease Create(
        ActivityId activityId,
        Guid messageId,
        DateTimeOffset releasedAtUtc)
    {
        return new LegacyActivityRelease(
            activityId,
            messageId,
            releasedAtUtc);
    }
}
