using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Activities;

public sealed record RequestKey
{
    public const int MaxLength = 100;

    private RequestKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RequestKey Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Request key is required.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new DomainValidationException(
                $"Request key cannot be longer than {MaxLength} characters.",
                nameof(value));
        }

        return new RequestKey(value);
    }

    public override string ToString() => Value;
}
