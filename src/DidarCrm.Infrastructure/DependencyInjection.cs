using DidarCrm.Application.Abstractions.Messaging;
using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Persistence;
using DidarCrm.Infrastructure.Persistence.Repositories;
using DidarCrm.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DidarCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<CrmDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
