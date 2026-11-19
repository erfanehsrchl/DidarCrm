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

    public DateTimeOffset? LastActivityAt { get; }
}
