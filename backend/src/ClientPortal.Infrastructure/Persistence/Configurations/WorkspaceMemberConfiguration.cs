using ClientPortal.Domain.Entities;
using ClientPortal.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");

        builder.HasKey(m => new { m.WorkspaceId, m.UserId });

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // The key starts with WorkspaceId; "my workspaces" looks members up by user, so it needs its own index.
        builder.HasIndex(m => m.UserId);

        // Deleting an account must not silently drop it from workspaces (an owner leaving needs a decision first).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
