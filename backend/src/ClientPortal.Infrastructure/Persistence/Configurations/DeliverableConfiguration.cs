using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> builder)
    {
        builder.ToTable("deliverables");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Title).HasMaxLength(Deliverable.TitleMaxLength).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(Deliverable.DescriptionMaxLength);
        builder.Property(d => d.Url).HasMaxLength(Deliverable.UrlMaxLength).IsRequired();
        builder.Ignore(d => d.IsArchived);

        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(d => new { d.WorkspaceId, d.ProjectId });

        // Restrict: projects and workspaces are archived, never deleted, so nothing here may disappear in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(d => d.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // Deleting a stage must not take its deliverables with it: they just lose the link.
        builder.HasOne<ProjectStage>().WithMany().HasForeignKey(d => d.StageId).OnDelete(DeleteBehavior.SetNull);
    }
}
