namespace ClientPortal.Application.Interfaces.Services;

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="Exceptions.NotFoundException"/> when nothing is stored under the key. The caller disposes the stream.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Best effort: never throws. A failure is logged and leaves an orphaned object, which is cheaper than failing the request.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
