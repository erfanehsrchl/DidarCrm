using System.Reflection;
using DidarCrm.Application.Activities.CreateActivity;
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

        foreach (var assembly in additionalValidatorAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }
}
