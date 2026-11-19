using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Common;

namespace DidarCrm.UnitTests.Domain.Activities;

public sealed class IdentifierTests
{
    [Fact]
    public void New_activity_id_is_not_empty()
    {
        var activityId = ActivityId.New();

        Assert.NotEqual(Guid.Empty, activityId.Value);
    }

    [Fact]
    public void Identifiers_preserve_their_values_and_use_value_equality()
    {
        var value = Guid.NewGuid();

        Assert.Equal(ActivityId.From(value), ActivityId.From(value));
        Assert.Equal(TenantId.From(value), TenantId.From(value));
        Assert.Equal(ContactId.From(value), ContactId.From(value));
        Assert.Equal(value, ActivityId.From(value).Value);
        Assert.Equal(value, TenantId.From(value).Value);
        Assert.Equal(value, ContactId.From(value).Value);
    }

    [Fact]
    public void Identifiers_reject_empty_guid()
    {
        Assert.Throws<DomainValidationException>(() => ActivityId.From(Guid.Empty));
        Assert.Throws<DomainValidationException>(() => TenantId.From(Guid.Empty));
        Assert.Throws<DomainValidationException>(() => ContactId.From(Guid.Empty));
    }
}
