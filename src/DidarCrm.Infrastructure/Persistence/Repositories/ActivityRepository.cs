using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Domain.Activities;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence.Repositories;

internal sealed class ActivityRepository(CrmDbContext dbContext)
    : IActivityRepository
{
    public Task<CustomerActivity?> GetByRequestKeyAsync(
        TenantId tenantId,
        RequestKey requestKey,
        CancellationToken cancellationToken)
    {
        return dbContext.Activities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                activity =>
                    activity.TenantId == tenantId &&
                    activity.RequestKey == requestKey,
                cancellationToken);
    }

    public async Task AddAsync(
        CustomerActivity activity,
        CancellationToken cancellationToken)
    {
        await dbContext.Activities.AddAsync(activity, cancellationToken);
    }
}
