using DidarCrm.Api.Contracts;
using DidarCrm.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace DidarCrm.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    [NonAction]
    public IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(true));
        }

        var response = ApiResponse<object>.Failure(
            GetMessage(result.Error),
            result.Error.Code);

        return MapError(result.Error.Type, response);
    }

    [NonAction]
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        return HandleResult(result, value => value);
    }

    [NonAction]
    protected IActionResult HandleResult<TSource, TResponse>(
        Result<TSource> result,
        Func<TSource, TResponse> map)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<TResponse>.Success(map(result.Value)));
        }

        var response = ApiResponse<TResponse>.Failure(
            GetMessage(result.Error),
            result.Error.Code);

        return MapError(result.Error.Type, response);
    }

    private IActionResult MapError<T>(
        ErrorType errorType,
        ApiResponse<T> response)
    {
        return errorType switch
        {
            ErrorType.Validation => BadRequest(response),
            ErrorType.NotFound => NotFound(response),
            ErrorType.Conflict => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }

    private static string GetMessage(Error error)
    {
        return error.Code switch
        {
            "Contact.NotFound" => "مخاطب موردنظر پیدا نشد.",
            "Activity.IdempotencyConflict" =>
                "این کلید درخواست قبلاً برای اطلاعات متفاوتی استفاده شده است.",
            _ => error.Message
        };
    }
}
