using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options)
{
    public DbSet<CustomerActivity> Activities => Set<CustomerActivity>();

    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
