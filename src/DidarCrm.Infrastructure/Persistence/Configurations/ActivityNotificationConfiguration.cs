using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DidarCrm.Infrastructure.Persistence.Configurations;

internal sealed class ActivityNotificationConfiguration
    : IEntityTypeConfiguration<ActivityNotification>
{
    internal const string ActivityIdUniqueIndexName =
        "UX_crmNotification_ActivityId";

    public void Configure(EntityTypeBuilder<ActivityNotification> builder)
    {
        builder.ToTable("crmNotification");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Id)
            .HasConversion(id => id.Value, value => NotificationId.From(value))
            .ValueGeneratedNever();

        builder.Property(notification => notification.ActivityId)
            .HasConversion(id => id.Value, value => ActivityId.From(value))
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.HasIndex(notification => notification.ActivityId)
            .IsUnique()
            .HasDatabaseName(ActivityIdUniqueIndexName);
    }
}
