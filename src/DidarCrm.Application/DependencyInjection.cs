using System.Reflection;
using DidarCrm.Application.Activities.CreateActivity;
using DidarCrm.Application.Backfill.ReleaseLegacyActivities;
using DidarCrm.Application.Notifications.CreateActivityNotification;
using DidarCrm.Application.Webhooks.SendActivityWebhook;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DidarCrm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        params Assembly[] additionalValidatorAssemblies)
    {
        services.AddScoped<CreateActivityHandler>();
        services.AddScoped<CreateActivityNotificationHandler>();
        services.AddScoped<SendActivityWebhookHandler>();
        services.AddScoped<ReleaseLegacyActivitiesHandler>();

        foreach (var assembly in additionalValidatorAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }
}
