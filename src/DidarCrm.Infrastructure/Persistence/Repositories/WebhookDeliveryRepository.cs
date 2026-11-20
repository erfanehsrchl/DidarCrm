using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence.Repositories;

internal sealed class WebhookDeliveryRepository(CrmDbContext dbContext)
    : IWebhookDeliveryRepository
{
    public Task<WebhookDelivery?> GetByActivityIdAsync(
        ActivityId activityId,
        CancellationToken cancellationToken)
    {
        return dbContext.WebhookDeliveries.SingleOrDefaultAsync(
            delivery => delivery.ActivityId == activityId,
            cancellationToken);
    }

    public void Add(WebhookDelivery delivery)
    {
        dbContext.WebhookDeliveries.Add(delivery);
    }
}
