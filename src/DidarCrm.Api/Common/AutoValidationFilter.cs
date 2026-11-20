using DidarCrm.Api.Controllers;
using DidarCrm.Application.Common.Results;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DidarCrm.Api.Common;

public sealed class AutoValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            await next();
            return;
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>)
                .MakeGenericType(argument.GetType());

            if (context.HttpContext.RequestServices.GetService(validatorType)
                is IValidator validator)
            {
                var validationResult = await validator.ValidateAsync(
                    new ValidationContext<object>(argument));

                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .Select(error => error.ErrorMessage)
                        .ToList();
                    var result = Result.Failure(
                        Error.Validation(string.Join(", ", errors)));
                    var controller = context.Controller as BaseController;

                    context.Result = controller?.HandleResult(result)
                        ?? new BadRequestObjectResult(errors);
                    return;
                }
            }
        }

        await next();
    }
}
