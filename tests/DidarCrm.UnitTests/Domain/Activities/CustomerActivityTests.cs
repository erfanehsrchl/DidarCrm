using DidarCrm.Domain.Activities;

namespace DidarCrm.UnitTests.Domain.Activities;

public sealed class CustomerActivityTests
{
    [Fact]
    public void Create_builds_activity_with_the_provided_utc_timestamp()
    {
        var tenantId = TenantId.From(Guid.NewGuid());
        var contactId = ContactId.From(Guid.NewGuid());
        var requestKey = RequestKey.Create("request-123");
        var title = ActivityTitle.Create("Follow up with customer");
        var createdAtUtc = new DateTimeOffset(2026, 11, 19, 11, 0, 0, TimeSpan.Zero);

        var activity = CustomerActivity.Create(
            tenantId,
            contactId,
            requestKey,
            title,
            createdAtUtc);

        Assert.NotEqual(Guid.Empty, activity.Id.Value);
        Assert.Equal(tenantId, activity.TenantId);
        Assert.Equal(contactId, activity.ContactId);
        Assert.Equal(requestKey, activity.RequestKey);
        Assert.Equal(title, activity.Title);
        Assert.Equal(createdAtUtc, activity.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, activity.CreatedAtUtc.Offset);
    }

    [Fact]
    public void Create_rejects_null_value_objects()
    {
        var tenantId = TenantId.From(Guid.NewGuid());
        var contactId = ContactId.From(Guid.NewGuid());
        var requestKey = RequestKey.Create("request-123");
        var title = ActivityTitle.Create("Follow up with customer");

        Assert.Throws<ArgumentNullException>(
            () => CustomerActivity.Create(null!, contactId, requestKey, title, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(
            () => CustomerActivity.Create(tenantId, null!, requestKey, title, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(
            () => CustomerActivity.Create(tenantId, contactId, null!, title, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(
            () => CustomerActivity.Create(tenantId, contactId, requestKey, null!, DateTimeOffset.UtcNow));
    }
}
