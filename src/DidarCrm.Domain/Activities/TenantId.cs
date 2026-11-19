using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Activities;

public sealed record TenantId
{
    private TenantId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Tenant id cannot be empty.", nameof(value));
        }

        return new TenantId(value);
    }

    public override string ToString() => Value.ToString();
}
