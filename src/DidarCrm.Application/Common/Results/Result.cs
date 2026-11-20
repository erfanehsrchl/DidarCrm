namespace DidarCrm.Application.Common.Results;

public sealed class Result
{
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error = null)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException(
            "A successful result does not have an error.");

    public static Result Success()
    {
        return new Result(true);
    }

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }
}

public sealed class Result<TValue>
{
    private readonly TValue? _value;
    private readonly Error? _error;

    private Result(TValue value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not have a value.");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("A successful result does not have an error.");

    public static Result<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<TValue>(value);
    }

    public static Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue>(error);
    }
}
