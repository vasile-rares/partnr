using ClientPortal.Domain.Entities;
using ClientPortal.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Company).HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.Ignore(c => c.IsArchived);
        builder.Ignore(c => c.IsLinked);

        // The same person can be a client of two different workspaces, but only once in each.
        builder.HasIndex(c => new { c.WorkspaceId, c.Email }).IsUnique();

        // An account is the client of a workspace at most once (unique, and NULLs, the unlinked cards, do not collide).
        builder.HasIndex(c => new { c.WorkspaceId, c.UserId }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);

        // Restrict: deleting a workspace must be a deliberate decision about its clients, not a silent cascade.
        builder.HasOne<Workspace>().WithMany().HasForeignKey(c => c.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
