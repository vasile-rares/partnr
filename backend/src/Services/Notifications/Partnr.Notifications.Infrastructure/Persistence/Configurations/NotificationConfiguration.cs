using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.EmailStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.RecipientEmail).HasMaxLength(Notification.EmailMaxLength).IsRequired();
        builder.Property(n => n.RecipientName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.WorkspaceName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.ProjectName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.DeliverableTitle).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.ActorName).HasMaxLength(Notification.NameMaxLength);
        builder.Property(n => n.EmailLastError).HasMaxLength(Notification.ErrorMaxLength);
        builder.Property(n => n.DedupeKey).HasMaxLength(120).IsRequired();

        // The same event delivered twice is one notification: the unique key is the last line of defence when two deliveries race.
        builder.HasIndex(n => n.DedupeKey, "IX_notifications_DedupeKey").IsUnique();

        // What the email worker asks for: pending rows that are due. Rows already sent are not in the index.
        builder.HasIndex(n => n.EmailNextAttemptAtUtc, "IX_notifications_email_due")
            .HasFilter("\"EmailStatus\" = 'Pending'");

        builder.HasIndex(n => n.ApprovalRequestId, "IX_notifications_ApprovalRequestId");

        // The inbox: an account's own notifications, newest first.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc }, "IX_notifications_inbox");
        builder.Ignore(n => n.IsRead);

        // The workspace, the account, the project, the client and the round are plain ids: they live in other services, and a
        // notification is a snapshot that outlives what it points at.
    }
}
