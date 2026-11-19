using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Activities;

public sealed record ActivityTitle
{
    public const int MaxLength = 500;

    private ActivityTitle(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ActivityTitle Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Activity title is required.", nameof(value));
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new DomainValidationException(
                $"Activity title cannot be longer than {MaxLength} characters.",
                nameof(value));
        }

        return new ActivityTitle(normalizedValue);
    }

    public override string ToString() => Value;
}
