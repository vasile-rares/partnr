using Partnr.Core.Domain.Entities;
using Partnr.Core.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Core.Infrastructure.Persistence.Configurations;

internal sealed class MemberInvitationConfiguration : IEntityTypeConfiguration<MemberInvitation>
{
    public void Configure(EntityTypeBuilder<MemberInvitation> builder)
    {
        builder.ToTable("member_invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Email).HasMaxLength(320).IsRequired();

        // SHA-256 in hex is always 64 characters. Looked up by this value, so it is unique.
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // An address has at most one open invitation (not accepted, not cancelled) per workspace; sending again cancels the previous one first.
        builder.HasIndex(i => new { i.WorkspaceId, i.Email })
            .HasDatabaseName("IX_member_invitations_open_per_email")
            .IsUnique()
            .HasFilter("\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

        builder.HasOne<Workspace>().WithMany().HasForeignKey(i => i.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
