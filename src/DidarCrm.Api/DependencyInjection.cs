using DidarCrm.Api.Common;
using DidarCrm.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DidarCrm.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services)
    {
        services
            .AddControllers(options =>
                options.Filters.Add<AutoValidationFilter>())
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                {
                    var hasMissingBody = context.ModelState.Keys.Any(
                        key => string.IsNullOrEmpty(key));
                    var message = hasMissingBody
                        ? "بدنه درخواست الزامی و باید معتبر باشد."
                        : "مقادیر ارسال‌شده معتبر نیستند.";

                    return new BadRequestObjectResult(
                        ApiResponse<object>.Failure(
                            message,
                            "Request.Validation"));
                });

        return services;
    }
}
