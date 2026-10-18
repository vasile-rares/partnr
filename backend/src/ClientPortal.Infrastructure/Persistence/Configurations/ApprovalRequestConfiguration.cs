using ClientPortal.Domain.Entities;
using ClientPortal.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Note).HasMaxLength(ApprovalRequest.NoteMaxLength);
        builder.Property(a => a.Message).HasMaxLength(ApprovalRequest.MessageMaxLength);
        builder.Ignore(a => a.IsPending);

        builder.HasIndex(a => new { a.WorkspaceId, a.ProjectId });
        builder.HasIndex(a => a.DeliverableId, "IX_approval_requests_DeliverableId");

        // A deliverable has at most one open round; the database refuses a second one even if two requests race.
        builder.HasIndex(a => a.DeliverableId, "IX_approval_requests_pending_per_deliverable")
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        // Restrict: a deliverable with approvals is archived, never deleted, so its history cannot go in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Deliverable>().WithMany().HasForeignKey(a => a.DeliverableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
