using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.Application.Interfaces.Repositories;

public interface IStoredFileRepository
{
    Task<StoredFile?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    void Add(StoredFile file);

    void Remove(StoredFile file);
}
