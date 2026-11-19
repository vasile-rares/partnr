using Microsoft.EntityFrameworkCore;
using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.Infrastructure.Persistence;

public sealed class StorageDbContext(DbContextOptions<StorageDbContext> options) : DbContext(options)
{
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);
}
