using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ProjectUpdateConfiguration : IEntityTypeConfiguration<ProjectUpdate>
{
    public void Configure(EntityTypeBuilder<ProjectUpdate> builder)
    {
        builder.ToTable("project_updates");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Title).HasMaxLength(ProjectUpdate.TitleMaxLength).IsRequired();
        builder.Property(u => u.Body).HasMaxLength(ProjectUpdate.BodyMaxLength);
        builder.Ignore(u => u.IsArchived);

        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => new { u.WorkspaceId, u.ProjectId });

        // Restrict: projects and workspaces are archived, never deleted, so nothing here may disappear in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(u => u.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(u => u.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
