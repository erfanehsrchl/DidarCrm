using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;

namespace DidarCrm.Application.Abstractions.Persistence;

public interface IContactRepository
{
    Task<Contact?> GetAsync(
        TenantId tenantId,
        ContactId contactId,
        CancellationToken cancellationToken);

    Task<bool> ReloadAsync(
        Contact contact,
        CancellationToken cancellationToken);
}
