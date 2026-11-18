using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Workspaces.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.EmailStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.WorkspaceName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.ProjectName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.DeliverableTitle).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.ActorName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.EmailLastError).HasMaxLength(Notification.ErrorMaxLength);

        // What the email worker asks for: pending rows that are due. Rows already sent are not in the index.
        builder.HasIndex(n => n.EmailNextAttemptAtUtc, "IX_notifications_email_due")
            .HasFilter("\"EmailStatus\" = 'Pending'");

        builder.HasIndex(n => n.ApprovalRequestId, "IX_notifications_ApprovalRequestId");

        // The inbox: an account's own notifications, newest first.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc }, "IX_notifications_inbox");
        builder.Ignore(n => n.IsRead);

        // The project, client and round are plain ids on purpose: a notification is a snapshot and outlives what it points at.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(n => n.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
