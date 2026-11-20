using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Abstractions.Webhooks;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;

namespace DidarCrm.Application.Webhooks.SendActivityWebhook;

public sealed class SendActivityWebhookHandler(
    IWebhookSender webhookSender,
    IWebhookDeliveryRepository deliveryRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task HandleAsync(
        SendActivityWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var activityId = ActivityId.From(command.ActivityId);
        var delivery = await deliveryRepository.GetByActivityIdAsync(
            activityId,
            cancellationToken);

        if (delivery?.Status == WebhookDeliveryStatus.Succeeded)
        {
            return;
        }

        if (delivery is null)
        {
            delivery = WebhookDelivery.Create(activityId, clock.UtcNow);
            deliveryRepository.Add(delivery);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DuplicateWebhookDeliveryException)
            {
                return;
            }
        }

        var payload = new ActivityWebhookPayload(
            command.ActivityId,
            command.TenantId,
            command.ContactId,
            command.Title,
            command.ActivityCreatedAtUtc);

        try
        {
            await webhookSender.SendAsync(
                command.ActivityId,
                payload,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException ||
                  !cancellationToken.IsCancellationRequested)
        {
            delivery.RecordFailure(exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        delivery.MarkSucceeded(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
