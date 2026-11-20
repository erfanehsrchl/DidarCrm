using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Webhooks;

public sealed record WebhookDeliveryId
{
    private WebhookDeliveryId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static WebhookDeliveryId New() => new(Guid.NewGuid());

    public static WebhookDeliveryId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException(
                "Webhook delivery id cannot be empty.",
                nameof(value));
        }

        return new WebhookDeliveryId(value);
    }
}
