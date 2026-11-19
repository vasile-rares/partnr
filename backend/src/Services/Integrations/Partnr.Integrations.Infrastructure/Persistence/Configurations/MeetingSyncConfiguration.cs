using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Partnr.Integrations.Domain.Entities;

namespace Partnr.Integrations.Infrastructure.Persistence.Configurations;

internal sealed class MeetingSyncConfiguration : IEntityTypeConfiguration<MeetingSync>
{
    public void Configure(EntityTypeBuilder<MeetingSync> builder)
    {
        builder.ToTable("meeting_syncs");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Title).HasMaxLength(MeetingSync.TitleMaxLength).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(MeetingSync.DescriptionMaxLength);
        builder.Property(m => m.ClientEmail).HasMaxLength(320).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.GoogleEventId).HasMaxLength(1024);
        builder.Property(m => m.MeetUrl).HasMaxLength(500);
        builder.Ignore(m => m.EventId);
        builder.Ignore(m => m.IsSettled);

        // A newer version that arrives while the sync talks to Google makes one of the two saves fail instead of the second silently undoing the first.
        builder.Property(m => m.Version).IsConcurrencyToken();
        builder.HasIndex(m => m.NextSyncAtUtc).HasFilter("\"Status\" = 'Pending'");

        // The organiser's account lives in another service: no foreign key.
        builder.HasIndex(m => m.OrganizerUserId);
    }
}
