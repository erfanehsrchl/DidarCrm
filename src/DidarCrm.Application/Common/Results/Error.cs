namespace DidarCrm.Application.Common.Results;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type)
{
    public static Error Validation(
        string message,
        string code = "Request.Validation")
    {
        return new Error(code, message, ErrorType.Validation);
    }
}
