using DidarCrm.Api.Contracts;

namespace DidarCrm.Api.Common;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unexpected error occurred.");

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Failure(
                    "خطای پیش‌بینی‌نشده‌ای رخ داد.",
                    "Server.Error"));
        }
    }
}
