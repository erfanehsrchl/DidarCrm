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

        Assert.Equal(activityId, delivery.ActivityId);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(createdAtUtc, delivery.CreatedAtUtc);
    }

    [Fact]
    public void MarkSucceeded_changes_pending_delivery_status()
    {
        var delivery = WebhookDelivery.Create(
            ActivityId.From(Guid.NewGuid()),
            new DateTimeOffset(
                2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        delivery.MarkSucceeded();

        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
    }
}
