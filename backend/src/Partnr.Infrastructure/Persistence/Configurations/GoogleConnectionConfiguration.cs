using Partnr.Domain.Entities;
using Partnr.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Infrastructure.Persistence.Configurations;

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

        // One connection per account.
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
