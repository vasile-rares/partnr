namespace Partnr.Storage.Application.Interfaces.Services;

/// <summary>The object store (any S3-compatible service). It never decides anything: it keeps bytes under a key.</summary>
public interface IBlobStore
{
    /// <param name="content">Seekable, so the store can sign and retry the upload.</param>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="Exceptions.NotFoundException"/> when nothing is stored under the key. The caller disposes the stream.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Throws when the store cannot be reached. Deleting something that is not there is not a failure.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
