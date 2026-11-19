using Partnr.Integrations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Integrations.Infrastructure.Persistence.Configurations;

internal sealed class GoogleConnectionConfiguration : IEntityTypeConfiguration<GoogleConnection>
{
    public void Configure(EntityTypeBuilder<GoogleConnection> builder)
    {
        builder.ToTable("google_connections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.GoogleEmail).HasMaxLength(GoogleConnection.EmailMaxLength).IsRequired();
        builder.Property(c => c.ProtectedRefreshToken).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One connection per account. The account lives in another service, so there is no foreign key to it.
        builder.HasIndex(c => c.UserId).IsUnique();
    }
}
