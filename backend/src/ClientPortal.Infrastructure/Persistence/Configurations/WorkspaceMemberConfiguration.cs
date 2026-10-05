using ClientPortal.Domain.Workspaces;
using ClientPortal.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");

        // A user is a member of a workspace at most once.
        builder.HasKey(m => new { m.WorkspaceId, m.UserId });

        // Stored as text so the database stays readable and reordering the enum cannot silently change roles.
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // The key starts with WorkspaceId; "my workspaces" looks members up by user, so it needs its own index.
        builder.HasIndex(m => m.UserId);

        // Deleting an account must not silently drop it from workspaces (an owner leaving needs a decision first).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
