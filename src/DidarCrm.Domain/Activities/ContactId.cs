using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Activities;

public sealed record ContactId
{
    private ContactId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static ContactId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Contact id cannot be empty.", nameof(value));
        }

        return new ContactId(value);
    }

    public override string ToString() => Value.ToString();
}
