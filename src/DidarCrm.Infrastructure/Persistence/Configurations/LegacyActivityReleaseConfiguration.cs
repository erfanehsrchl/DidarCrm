using DidarCrm.Domain.Activities;
using DidarCrm.Infrastructure.Backfill;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DidarCrm.Infrastructure.Persistence.Configurations;

internal sealed class LegacyActivityReleaseConfiguration
    : IEntityTypeConfiguration<LegacyActivityRelease>
{
    public void Configure(EntityTypeBuilder<LegacyActivityRelease> builder)
    {
        builder.ToTable("crmActivityBackfillRelease");

        builder.HasKey(release => release.ActivityId);

        builder.Property(release => release.ActivityId)
            .HasConversion(id => id.Value, value => ActivityId.From(value))
            .ValueGeneratedNever();

        builder.Property(release => release.MessageId)
            .IsRequired();

        builder.Property(release => release.ReleasedAtUtc)
            .HasColumnName("ReleasedAt")
            .IsRequired();

        builder.HasIndex(release => release.MessageId)
            .IsUnique();
    }
}
