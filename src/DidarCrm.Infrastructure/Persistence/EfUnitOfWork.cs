using DidarCrm.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CrmDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException(exception);
        }
    }
}
