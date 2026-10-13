using ClientPortal.Domain.Entities;
using ClientPortal.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClientPortal.Infrastructure.Persistence.Configurations;

internal sealed class ClientInvitationConfiguration : IEntityTypeConfiguration<ClientInvitation>
{
    public void Configure(EntityTypeBuilder<ClientInvitation> builder)
    {
        builder.ToTable("client_invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Email).HasMaxLength(320).IsRequired();

        // SHA-256 in hex is always 64 characters. Looked up by this value, so it is unique.
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // A client has at most one open invitation (not accepted, not cancelled); sending again cancels the previous one first.
        builder.HasIndex(i => i.ClientId)
            .HasDatabaseName("IX_client_invitations_open_per_client")
            .IsUnique()
            .HasFilter("\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

        // Restrict, like clients: nothing disappears through a silent cascade.
        builder.HasOne<Client>().WithMany().HasForeignKey(i => i.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Workspace>().WithMany().HasForeignKey(i => i.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
