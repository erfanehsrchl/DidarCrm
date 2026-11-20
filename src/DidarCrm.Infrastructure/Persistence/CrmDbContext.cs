using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Domain.Notifications;
using DidarCrm.Domain.Webhooks;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options)
{
    public DbSet<CustomerActivity> Activities => Set<CustomerActivity>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<ActivityNotification> Notifications =>
        Set<ActivityNotification>();

    public DbSet<WebhookDelivery> WebhookDeliveries =>
        Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
