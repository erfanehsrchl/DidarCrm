using DidarCrm.Domain.Activities;

namespace DidarCrm.Domain.Webhooks;

public sealed class WebhookDelivery
{
    public const int MaxErrorLength = 2000;

    private WebhookDelivery(
        WebhookDeliveryId id,
        ActivityId activityId,
        WebhookDeliveryStatus status,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ActivityId = activityId;
        CreatedAtUtc = createdAtUtc;
        Status = status;
    }

    public WebhookDeliveryId Id { get; }

    public ActivityId ActivityId { get; }

    public WebhookDeliveryStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public int AttemptCount { get; private set; }

    public static WebhookDelivery Create(
        ActivityId activityId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activityId);

        return new WebhookDelivery(
            WebhookDeliveryId.New(),
            activityId,
            WebhookDeliveryStatus.Pending,
            createdAtUtc);
    }

    public void MarkSucceeded(DateTimeOffset attemptedAtUtc)
    {
        AttemptCount++;
        Status = WebhookDeliveryStatus.Succeeded;
        LastAttemptAtUtc = attemptedAtUtc;
        LastError = null;
    }

    public void RecordFailure(
        string error,
        DateTimeOffset attemptedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        AttemptCount++;
        Status = WebhookDeliveryStatus.Pending;
        LastAttemptAtUtc = attemptedAtUtc;
        LastError = error.Length <= MaxErrorLength
            ? error
            : error[..MaxErrorLength];
    }
}
