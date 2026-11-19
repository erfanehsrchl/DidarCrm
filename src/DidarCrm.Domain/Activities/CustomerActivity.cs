namespace DidarCrm.Domain.Activities;

public sealed class CustomerActivity
{
    private CustomerActivity(
        ActivityId id,
        TenantId tenantId,
        ContactId contactId,
        RequestKey requestKey,
        ActivityTitle title,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ContactId = contactId;
        RequestKey = requestKey;
        Title = title;
        CreatedAtUtc = createdAtUtc;
    }

    public ActivityId Id { get; }

    public TenantId TenantId { get; }

    public ContactId ContactId { get; }

    public RequestKey RequestKey { get; }

    public ActivityTitle Title { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static CustomerActivity Create(
        TenantId tenantId,
        ContactId contactId,
        RequestKey requestKey,
        ActivityTitle title,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(contactId);
        ArgumentNullException.ThrowIfNull(requestKey);
        ArgumentNullException.ThrowIfNull(title);

        return new CustomerActivity(
            ActivityId.New(),
            tenantId,
            contactId,
            requestKey,
            title,
            createdAtUtc);
    }
}
