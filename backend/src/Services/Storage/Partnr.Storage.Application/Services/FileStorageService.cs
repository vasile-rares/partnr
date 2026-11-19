using System.Security.Cryptography;
using Partnr.Storage.Application.Exceptions;
using Partnr.Storage.Application.Interfaces.Repositories;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.Application.Services;

internal sealed class FileStorageService(
    IBlobStore blobs,
    IStoredFileRepository files,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IFileStorageService
{
    public async Task<long> SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // A file is at most 25 MB, so it is held in memory: the object store needs a seekable body, and the checksum is made from it.
        using var buffer = new MemoryStream();
        await CopyWithinLimitAsync(content, buffer, cancellationToken);
        var size = buffer.Length;
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)size)));
        buffer.Position = 0;

        await blobs.PutAsync(key, buffer, contentType, cancellationToken);

        var now = time.GetUtcNow().UtcDateTime;
        var existing = await files.GetByKeyAsync(key, cancellationToken);
        if (existing is null)
        {
            files.Add(StoredFile.Create(key, size, contentType, sha256, now));
        }
        else
        {
            existing.Replace(size, contentType, sha256, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return size;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        blobs.OpenReadAsync(key, cancellationToken);

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await blobs.DeleteAsync(key, cancellationToken);

        var existing = await files.GetByKeyAsync(key, cancellationToken);
        if (existing is not null)
        {
            files.Remove(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task CopyWithinLimitAsync(Stream from, Stream to, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        int read;
        while ((read = await from.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (to.Length + read > StorageLimits.MaxFileBytes)
            {
                throw new ValidationFailedException($"A file may weigh at most {StorageLimits.MaxFileBytes / (1024 * 1024)} MB.");
            }

            await to.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
