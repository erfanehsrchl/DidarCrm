using DidarCrm.Application.Activities.Events;
using DidarCrm.Application.Webhooks.SendActivityWebhook;
using DidarCrm.Infrastructure.Messaging;
using MassTransit;

namespace DidarCrm.Api.Consumers.Webhooks;

internal sealed class ActivityCreatedWebhookConsumer(
    SendActivityWebhookHandler handler,
    WebhookRetryPolicy retryPolicy)
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
                message.OccurredAtUtc,
                context.GetRetryAttempt() >= retryPolicy.RetryCount),
            context.CancellationToken);
    }
}
