using DidarCrm.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options), IUnitOfWork
{
    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }
}
