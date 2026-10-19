using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ProjectFileConfiguration : IEntityTypeConfiguration<ProjectFile>
{
    public void Configure(EntityTypeBuilder<ProjectFile> builder)
    {
        builder.ToTable("project_files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Name).HasMaxLength(ProjectFile.NameMaxLength).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(ProjectFile.DescriptionMaxLength);
        builder.Property(f => f.FileName).HasMaxLength(ProjectFile.FileNameMaxLength).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Ignore(f => f.IsArchived);
        builder.Ignore(f => f.StorageKey);

        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(f => new { f.WorkspaceId, f.ProjectId });

        // Restrict: projects and workspaces are archived, never deleted, so nothing here may disappear in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(f => f.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
