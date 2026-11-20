using DidarCrm.Infrastructure.Messaging;
using MassTransit;

namespace DidarCrm.Api.Consumers.Notifications;

internal sealed class ActivityCreatedNotificationConsumerDefinition
    : ConsumerDefinition<ActivityCreatedNotificationConsumer>
{
    public ActivityCreatedNotificationConsumerDefinition()
    {
        EndpointName = MessagingTopology.NotificationQueue;
    }
}
