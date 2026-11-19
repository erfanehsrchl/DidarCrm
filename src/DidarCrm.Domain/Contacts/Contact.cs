using DidarCrm.Domain.Activities;

namespace DidarCrm.Domain.Contacts;

public sealed class Contact
{
    private Contact(
        ContactId id,
        TenantId tenantId,
        DateTimeOffset? lastActivityAt)
    {
        Id = id;
        TenantId = tenantId;
        LastActivityAt = lastActivityAt;
    }

    public ContactId Id { get; }

    public TenantId TenantId { get; }

    public DateTimeOffset? LastActivityAt { get; private set; }

    public static Contact Create(
        ContactId id,
        TenantId tenantId,
        DateTimeOffset? lastActivityAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tenantId);

        return new Contact(id, tenantId, lastActivityAt);
    }

    public void AdvanceLastActivityAt(DateTimeOffset activityCreatedAtUtc)
    {
        if (LastActivityAt is null || activityCreatedAtUtc > LastActivityAt)
        {
            LastActivityAt = activityCreatedAtUtc;
        }
    }
}
