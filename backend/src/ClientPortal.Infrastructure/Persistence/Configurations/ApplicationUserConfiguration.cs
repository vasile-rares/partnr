using ClientPortal.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();

        // Identity creates this index as non-unique; making it unique closes the race between two concurrent registrations.
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
