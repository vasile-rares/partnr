using Partnr.Domain.Entities;
using Partnr.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Infrastructure.Persistence.Configurations;

internal sealed class ConversationReadConfiguration : IEntityTypeConfiguration<ConversationRead>
{
    public void Configure(EntityTypeBuilder<ConversationRead> builder)
    {
        builder.ToTable("conversation_reads");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // One read mark per person per conversation.
        builder.HasIndex(r => new { r.UserId, r.ProjectId }).IsUnique();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
