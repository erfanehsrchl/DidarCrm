using DidarCrm.Domain.Activities;

namespace DidarCrm.Domain.Notifications;

public sealed class ActivityNotification
{
    private ActivityNotification(
        NotificationId id,
        ActivityId activityId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ActivityId = activityId;
        CreatedAtUtc = createdAtUtc;
    }

    public NotificationId Id { get; }

    public ActivityId ActivityId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static ActivityNotification Create(
        ActivityId activityId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activityId);

        return new ActivityNotification(
            NotificationId.New(),
            activityId,
            createdAtUtc);
    }
}
