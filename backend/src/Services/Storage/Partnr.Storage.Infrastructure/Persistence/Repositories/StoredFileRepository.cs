using Microsoft.EntityFrameworkCore;
using Partnr.Storage.Application.Interfaces.Repositories;
using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.Infrastructure.Persistence.Repositories;

internal sealed class StoredFileRepository(StorageDbContext dbContext) : IStoredFileRepository
{
    public Task<StoredFile?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        dbContext.StoredFiles.FirstOrDefaultAsync(f => f.Key == key, cancellationToken);

    public void Add(StoredFile file) => dbContext.StoredFiles.Add(file);

    public void Remove(StoredFile file) => dbContext.StoredFiles.Remove(file);
}
