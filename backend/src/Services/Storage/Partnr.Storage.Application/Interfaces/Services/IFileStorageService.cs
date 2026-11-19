namespace Partnr.Storage.Application.Interfaces.Services;

public interface IFileStorageService
{
    /// <summary>Keeps the bytes under the key (replacing what was there) and returns their size. Refuses more than <see cref="StorageLimits.MaxFileBytes"/>.</summary>
    Task<long> SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="Exceptions.NotFoundException"/> when nothing is stored under the key. The caller disposes the stream.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the bytes and their record. Safe to repeat; throws when the object store cannot be reached, so a caller can try again.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
