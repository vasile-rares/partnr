using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Workspaces.Infrastructure.Persistence.Configurations;

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Title).HasMaxLength(Meeting.TitleMaxLength).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(Meeting.DescriptionMaxLength);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(m => m.IsCancelled);

        builder.Property(m => m.SyncStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.MeetUrl).HasMaxLength(500);
        // A change made while an answer from Integrations is applied makes one of the two saves fail instead of the second silently undoing the first.
        builder.Property(m => m.Version).IsConcurrencyToken();

        builder.HasIndex(m => new { m.WorkspaceId, m.StartsAtUtc });
        builder.HasIndex(m => m.ClientId);

        // Restrict: workspaces, clients and projects are archived, never deleted, and a meeting is history that must outlive an account.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(m => m.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(m => m.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
