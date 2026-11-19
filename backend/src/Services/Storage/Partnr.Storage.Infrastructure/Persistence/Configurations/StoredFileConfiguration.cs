using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.Infrastructure.Persistence.Configurations;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Key).IsRequired().HasMaxLength(StoredFile.MaxKeyLength);
        builder.HasIndex(f => f.Key).IsUnique();

        builder.Property(f => f.ContentType).IsRequired().HasMaxLength(255);
        builder.Property(f => f.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();
    }
}
