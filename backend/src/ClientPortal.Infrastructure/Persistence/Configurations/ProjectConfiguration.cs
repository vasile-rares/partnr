using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Project.DescriptionMaxLength);
        builder.Ignore(p => p.IsArchived);

        // Stored as text so the database stays readable and reordering the enum cannot silently change statuses.
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // Stages belong to the aggregate: loaded and saved with it, through the private _stages field, and gone with it.
        builder.HasMany(p => p.Stages).WithOne().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Stages).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(p => p.DoneStageCount);

        // The list and a client's page both filter by workspace first, then by client.
        builder.HasIndex(p => new { p.WorkspaceId, p.ClientId });

        // Restrict: archiving is how clients and workspaces go away, so nothing here may disappear in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(p => p.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
    }
}
