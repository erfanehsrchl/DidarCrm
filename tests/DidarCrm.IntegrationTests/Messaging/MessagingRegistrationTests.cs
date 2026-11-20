using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Infrastructure;
using DidarCrm.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace DidarCrm.IntegrationTests.Messaging;

public sealed class MessagingRegistrationTests
{
    [Fact]
    public async Task AddInfrastructure_RegistersPublisherAndMassTransitEndpoint()
    {
        await using var serviceProvider = new ServiceCollection()
            .AddInfrastructure(
                "Server=localhost;Database=unused;Trusted_Connection=True;",
                new RabbitMqOptions { Enabled = false })
            .BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        Assert.NotNull(
            scope.ServiceProvider.GetService<IPublishEndpoint>());
        Assert.NotNull(
            scope.ServiceProvider.GetService<IIntegrationEventPublisher>());
    }
}
