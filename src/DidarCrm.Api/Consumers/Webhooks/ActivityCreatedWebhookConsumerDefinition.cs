using DidarCrm.Infrastructure.Messaging;
using MassTransit;

namespace DidarCrm.Api.Consumers.Webhooks;

internal sealed class ActivityCreatedWebhookConsumerDefinition
    : ConsumerDefinition<ActivityCreatedWebhookConsumer>
{
    public ActivityCreatedWebhookConsumerDefinition()
    {
        EndpointName = MessagingTopology.WebhookQueue;
    }
}
