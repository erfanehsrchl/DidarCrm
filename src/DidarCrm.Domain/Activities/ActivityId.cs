using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Activities;

public sealed record ActivityId
{
    private ActivityId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static ActivityId New() => new(Guid.NewGuid());

    public static ActivityId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Activity id cannot be empty.", nameof(value));
        }

        return new ActivityId(value);
    }

    public override string ToString() => Value.ToString();
}
