using DidarCrm.Application.Activities.Events;
using DidarCrm.Application.Notifications.CreateActivityNotification;
using MassTransit;

namespace DidarCrm.Api.Consumers.Notifications;

internal sealed class ActivityCreatedNotificationConsumer(
    CreateActivityNotificationHandler handler)
    : IConsumer<ActivityCreatedEvent>
{
    public Task Consume(ConsumeContext<ActivityCreatedEvent> context)
    {
        var message = context.Message;

        return handler.HandleAsync(
            new CreateActivityNotificationCommand(message.ActivityId),
            context.CancellationToken);
    }
}
