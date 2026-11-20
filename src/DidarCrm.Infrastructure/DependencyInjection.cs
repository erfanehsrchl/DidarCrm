using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Activities.Events;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Persistence;
using DidarCrm.Infrastructure.Persistence.Repositories;
using DidarCrm.Infrastructure.Time;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace DidarCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        RabbitMqOptions? rabbitMqOptions = null)
    {
        rabbitMqOptions ??= new RabbitMqOptions();

        services.AddDbContext<CrmDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMassTransit(configurator =>
        {
            configurator.AddEntityFrameworkOutbox<CrmDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.UseBusOutbox(busOutbox =>
                    busOutbox.DisableDeliveryService());
            });

            if (!rabbitMqOptions.Enabled)
            {
                configurator.UsingInMemory((context, bus) =>
                    bus.ConfigureEndpoints(context));
                return;
            }

            configurator.UsingRabbitMq((_, bus) =>
            {
                bus.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.VirtualHost,
                    host =>
                    {
                        host.Username(rabbitMqOptions.Username);
                        host.Password(rabbitMqOptions.Password);
                    });

                bus.Message<ActivityCreatedEvent>(message =>
                    message.SetEntityName(
                        MessagingTopology.ActivityCreatedExchange));
                bus.Publish<ActivityCreatedEvent>(publish =>
                    publish.ExchangeType = ExchangeType.Fanout);
            });
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
