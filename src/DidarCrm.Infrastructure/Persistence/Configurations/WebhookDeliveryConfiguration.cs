using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DidarCrm.Infrastructure.Persistence.Configurations;

internal sealed class WebhookDeliveryConfiguration
    : IEntityTypeConfiguration<WebhookDelivery>
{
    internal const string ActivityIdUniqueIndexName =
        "UX_crmWebhookDelivery_ActivityId";

    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("crmWebhookDelivery");

        builder.HasKey(delivery => delivery.Id);

        builder.Property(delivery => delivery.Id)
            .HasConversion(id => id.Value, value => WebhookDeliveryId.From(value))
            .ValueGeneratedNever();

        builder.Property(delivery => delivery.ActivityId)
            .HasConversion(id => id.Value, value => ActivityId.From(value))
            .ValueGeneratedNever();

        builder.HasIndex(delivery => delivery.ActivityId)
            .IsUnique()
            .HasDatabaseName(ActivityIdUniqueIndexName);

        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(delivery => delivery.CreatedAtUtc)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(delivery => delivery.LastAttemptAtUtc)
            .HasColumnName("LastAttemptAt");

        builder.Property(delivery => delivery.LastError)
            .HasMaxLength(WebhookDelivery.MaxErrorLength);
    }
}
