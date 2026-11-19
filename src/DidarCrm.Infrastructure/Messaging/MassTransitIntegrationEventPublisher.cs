using DidarCrm.Application.Abstractions.Messaging;
using MassTransit;

namespace DidarCrm.Infrastructure.Messaging;

internal sealed class MassTransitIntegrationEventPublisher(
    IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        return publishEndpoint.Publish(
            integrationEvent,
            integrationEvent.GetType(),
            cancellationToken);
    }
}
