using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Notifications;

namespace DidarCrm.UnitTests.Domain.Notifications;

public sealed class ActivityNotificationTests
{
    [Fact]
    public void Create_preserves_activity_id_and_notification_time()
    {
        var activityId = ActivityId.From(Guid.NewGuid());
        var notificationCreatedAtUtc = new DateTimeOffset(
            2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

        var notification = ActivityNotification.Create(
            activityId,
            notificationCreatedAtUtc);

        Assert.NotEqual(Guid.Empty, notification.Id.Value);
        Assert.Equal(activityId, notification.ActivityId);
        Assert.Equal(notificationCreatedAtUtc, notification.CreatedAtUtc);
    }
}
