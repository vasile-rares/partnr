using System.Collections.Concurrent;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;

namespace ClientPortal.IntegrationTests;

/// <summary>Keeps the uploaded bytes in memory, so the API tests need no object storage. <c>S3FileStorageTests</c> covers the real one.</summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public bool Contains(string key) => _objects.ContainsKey(key);

    public int CountUnder(string prefix) => _objects.Keys.Count(k => k.StartsWith(prefix, StringComparison.Ordinal));

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        _objects[key] = copy.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        _objects.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new NotFoundException("The file's content was not found.");

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
