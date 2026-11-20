using DidarCrm.Application.Activities.Events;
using DidarCrm.Application.Webhooks.SendActivityWebhook;
using MassTransit;

namespace DidarCrm.Api.Consumers.Webhooks;

internal sealed class ActivityCreatedWebhookConsumer(
    SendActivityWebhookHandler handler)
    : IConsumer<ActivityCreatedEvent>
{
    public Task Consume(ConsumeContext<ActivityCreatedEvent> context)
    {
        var message = context.Message;

        return handler.HandleAsync(
            new SendActivityWebhookCommand(
                message.ActivityId,
                message.TenantId,
                message.ContactId,
                message.Title,
                message.OccurredAtUtc),
            context.CancellationToken);
    }
}
