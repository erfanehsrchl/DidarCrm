using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;

namespace DidarCrm.Application.Abstractions.Persistence;

public interface IWebhookDeliveryRepository
{
    Task<WebhookDelivery?> GetByActivityIdAsync(
        ActivityId activityId,
        CancellationToken cancellationToken);

    void Add(WebhookDelivery delivery);
}
