using DidarCrm.Domain.Activities;

namespace DidarCrm.Application.Abstractions.Persistence;

public interface IActivityRepository
{
    Task<CustomerActivity?> GetByRequestKeyAsync(
        TenantId tenantId,
        RequestKey requestKey,
        CancellationToken cancellationToken);

    void Add(CustomerActivity activity);
}
