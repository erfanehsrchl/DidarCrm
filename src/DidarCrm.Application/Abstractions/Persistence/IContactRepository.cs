using DidarCrm.Domain.Activities;

namespace DidarCrm.Application.Abstractions.Persistence;

public interface IContactRepository
{
    Task<bool> AdvanceLastActivityAtAsync(
        TenantId tenantId,
        ContactId contactId,
        DateTimeOffset activityCreatedAtUtc,
        CancellationToken cancellationToken);
}
