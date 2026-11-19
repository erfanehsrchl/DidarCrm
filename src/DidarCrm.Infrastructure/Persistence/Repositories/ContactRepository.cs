using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository(CrmDbContext dbContext)
    : IContactRepository
{
    public Task<Contact?> GetAsync(
        TenantId tenantId,
        ContactId contactId,
        CancellationToken cancellationToken)
    {
        return dbContext.Contacts.SingleOrDefaultAsync(
            contact =>
                contact.TenantId == tenantId &&
                contact.Id == contactId,
            cancellationToken);
    }

    public async Task<bool> ReloadAsync(
        Contact contact,
        CancellationToken cancellationToken)
    {
        var entry = dbContext.Entry(contact);

        await entry.ReloadAsync(cancellationToken);

        return entry.State != EntityState.Detached;
    }
}
