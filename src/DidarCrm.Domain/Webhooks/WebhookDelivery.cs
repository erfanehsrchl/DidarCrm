using DidarCrm.Domain.Activities;

namespace DidarCrm.Domain.Webhooks;

public sealed class WebhookDelivery
{
    private WebhookDelivery(
        ActivityId activityId,
        DateTimeOffset createdAtUtc)
    {
        ActivityId = activityId;
        CreatedAtUtc = createdAtUtc;
        Status = WebhookDeliveryStatus.Pending;
    }

    public ActivityId ActivityId { get; }

    public WebhookDeliveryStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static WebhookDelivery Create(
        ActivityId activityId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activityId);

        return new WebhookDelivery(activityId, createdAtUtc);
    }

    public void MarkSucceeded()
    {
        Status = WebhookDeliveryStatus.Succeeded;
    }
}
