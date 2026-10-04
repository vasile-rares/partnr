using ClientPortal.Domain.Clients;
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

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
    }
}
