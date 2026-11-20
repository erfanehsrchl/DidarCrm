using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;

namespace DidarCrm.UnitTests.Domain.Webhooks;

public sealed class WebhookDeliveryTests
{
    [Fact]
    public void Create_starts_delivery_as_pending()
    {
        var activityId = ActivityId.From(Guid.NewGuid());
        var createdAtUtc = new DateTimeOffset(
            2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

        var delivery = WebhookDelivery.Create(activityId, createdAtUtc);

        Assert.NotEqual(Guid.Empty, delivery.Id.Value);
        Assert.Equal(activityId, delivery.ActivityId);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(createdAtUtc, delivery.CreatedAtUtc);
        Assert.Equal(0, delivery.AttemptCount);
    }

    [Fact]
    public void MarkSucceeded_changes_pending_delivery_status()
    {
        var delivery = WebhookDelivery.Create(
            ActivityId.From(Guid.NewGuid()),
            new DateTimeOffset(
                2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        var attemptedAtUtc = new DateTimeOffset(
            2026, 11, 20, 10, 1, 0, TimeSpan.Zero);

        delivery.MarkSucceeded(attemptedAtUtc);

        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(attemptedAtUtc, delivery.LastAttemptAtUtc);
        Assert.Null(delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public void RecordFailure_keeps_delivery_pending_and_stores_error()
    {
        var delivery = WebhookDelivery.Create(
            ActivityId.From(Guid.NewGuid()),
            new DateTimeOffset(
                2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
        var attemptedAtUtc = new DateTimeOffset(
            2026, 11, 20, 10, 1, 0, TimeSpan.Zero);

        delivery.RecordFailure("Destination unavailable", attemptedAtUtc);

        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(attemptedAtUtc, delivery.LastAttemptAtUtc);
        Assert.Equal("Destination unavailable", delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public void MarkFailed_marks_delivery_as_permanently_failed()
    {
        var delivery = WebhookDelivery.Create(
            ActivityId.From(Guid.NewGuid()),
            new DateTimeOffset(
                2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
        var attemptedAtUtc = new DateTimeOffset(
            2026, 11, 20, 10, 1, 0, TimeSpan.Zero);

        delivery.MarkFailed("Destination unavailable", attemptedAtUtc);

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(attemptedAtUtc, delivery.LastAttemptAtUtc);
        Assert.Equal("Destination unavailable", delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
    }
}
