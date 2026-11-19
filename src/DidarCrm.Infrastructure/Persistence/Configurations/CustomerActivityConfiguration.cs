using DidarCrm.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DidarCrm.Infrastructure.Persistence.Configurations;

internal sealed class CustomerActivityConfiguration
    : IEntityTypeConfiguration<CustomerActivity>
{
    internal const string RequestKeyUniqueIndexName =
        "UX_crmActivity_TenantId_RequestKey";

    public void Configure(EntityTypeBuilder<CustomerActivity> builder)
    {
        builder.ToTable("crmActivity");

        builder.HasKey(activity => activity.Id);

        builder.Property(activity => activity.Id)
            .HasConversion(id => id.Value, value => ActivityId.From(value))
            .ValueGeneratedNever();

        builder.Property(activity => activity.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(activity => activity.ContactId)
            .HasConversion(id => id.Value, value => ContactId.From(value))
            .IsRequired();

        builder.Property(activity => activity.RequestKey)
            .HasConversion(key => key.Value, value => RequestKey.Create(value))
            .HasMaxLength(RequestKey.MaxLength)
            .IsRequired(false);

        builder.Property(activity => activity.Title)
            .HasConversion(title => title.Value, value => ActivityTitle.Create(value))
            .HasMaxLength(ActivityTitle.MaxLength)
            .IsRequired();

        builder.Property(activity => activity.CreatedAtUtc)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.HasIndex(activity => new
            {
                activity.TenantId,
                activity.RequestKey
            })
            .IsUnique()
            .HasFilter("[RequestKey] IS NOT NULL")
            .HasDatabaseName(RequestKeyUniqueIndexName);

        builder.Property<bool>("WebhookSent")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property<bool>("NotificationCreated")
            .HasDefaultValue(false)
            .IsRequired();
    }
}
