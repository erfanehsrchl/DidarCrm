using System.Text.Json.Serialization;

namespace DidarCrm.Api.Contracts;

public sealed class ApiResponse<T>
{
    public ApiResponse(
        bool isSuccess,
        T? data,
        string? message,
        string? code)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
        Code = code;
    }

    public bool IsSuccess { get; }

    public T? Data { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; }

    public static ApiResponse<T> Success(T data)
    {
        return new ApiResponse<T>(true, data, null, null);
    }

    public static ApiResponse<T> Failure(string message, string code)
    {
        return new ApiResponse<T>(false, default, message, code);
    }
}
