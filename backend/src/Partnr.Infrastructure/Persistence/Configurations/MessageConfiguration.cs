using Partnr.Domain.Entities;
using Partnr.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Partnr.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Body).HasMaxLength(Message.BodyMaxLength).IsRequired();
        builder.Ignore(m => m.IsDeleted);

        // A conversation is read newest first, a page at a time.
        builder.HasIndex(m => new { m.ProjectId, m.CreatedAtUtc });

        // Restrict: projects and accounts are archived, not deleted, so a conversation cannot vanish in a cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(m => m.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
