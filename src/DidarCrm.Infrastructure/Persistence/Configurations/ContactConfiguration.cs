using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DidarCrm.Infrastructure.Persistence.Configurations;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("cntContact");

        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.Id)
            .HasConversion(id => id.Value, value => ContactId.From(value))
            .ValueGeneratedNever();

        builder.Property(contact => contact.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(contact => contact.LastActivityAt)
            .IsConcurrencyToken();
    }
}
