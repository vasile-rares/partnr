using Partnr.Workspaces.Domain.Common;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Workspaces.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TokenHash).HasMaxLength(SecureToken.HashLength).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.Ignore(t => t.IsOpen);

        // At most one open link per account; asking again cancels the previous one first.
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_password_reset_tokens_open_per_user")
            .IsUnique()
            .HasFilter("\"UsedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
