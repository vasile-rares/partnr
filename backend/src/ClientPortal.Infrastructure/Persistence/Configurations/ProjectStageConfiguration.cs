using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ProjectStageConfiguration : IEntityTypeConfiguration<ProjectStage>
{
    public void Configure(EntityTypeBuilder<ProjectStage> builder)
    {
        builder.ToTable("project_stages");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(ProjectStage.NameMaxLength).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(ProjectStage.DescriptionMaxLength);

        // Stored as text so the database stays readable and reordering the enum cannot silently change statuses.
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        // Deliberately not unique on (ProjectId, Position): swapping two stages changes both positions in one save, and the
        // database would see the same position twice halfway through. The project keeps the positions 0, 1, 2... itself.
        builder.HasIndex(s => s.ProjectId);
    }
}
