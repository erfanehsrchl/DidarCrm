using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<CustomerActivity> Activities => Set<CustomerActivity>();

    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }
}
