using System.Security.Cryptography;
using Partnr.Storage.Application;
using Partnr.Storage.Application.Exceptions;
using Partnr.Storage.Application.Interfaces.Repositories;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Application.Services;
using Partnr.Storage.Domain.Entities;

namespace Partnr.Storage.UnitTests.Application;

public class FileStorageServiceTests
{
    private const string Key = "workspace/project/file";

    private readonly FakeBlobStore _blobs = new();
    private readonly FakeStoredFileRepository _files = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FileStorageService _service;

    public FileStorageServiceTests() => _service = new FileStorageService(_blobs, _files, _unitOfWork, TimeProvider.System);

    [Fact]
    public async Task Save_KeepsTheBytes_AndRecordsTheirSizeAndChecksum()
    {
        byte[] bytes = [1, 2, 3, 4];

        var size = await _service.SaveAsync(Key, new MemoryStream(bytes), "application/pdf", CancellationToken.None);

        Assert.Equal(4, size);
        Assert.Equal(bytes, _blobs.Objects[Key]);
        var record = Assert.Single(_files.Items);
        Assert.Equal(Key, record.Key);
        Assert.Equal(4, record.SizeBytes);
        Assert.Equal("application/pdf", record.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), record.Sha256);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Save_UnderAnExistingKey_ReplacesTheRecordInsteadOfAddingOne()
    {
        await _service.SaveAsync(Key, new MemoryStream([1, 2, 3]), "application/zip", CancellationToken.None);

        await _service.SaveAsync(Key, new MemoryStream([7]), "text/plain", CancellationToken.None);

        var record = Assert.Single(_files.Items);
        Assert.Equal(1, record.SizeBytes);
        Assert.Equal("text/plain", record.ContentType);
        Assert.Equal<byte[]>([7], _blobs.Objects[Key]);
    }

    [Fact]
    public async Task Save_OfAFileOverTheLimit_StoresNothing()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.SaveAsync(Key, new MemoryStream(new byte[StorageLimits.MaxFileBytes + 1]), "application/zip", CancellationToken.None));

        Assert.Empty(_blobs.Objects);
        Assert.Empty(_files.Items);
    }

    [Fact]
    public async Task Save_OfAFileExactlyAtTheLimit_IsAccepted()
    {
        var size = await _service.SaveAsync(Key, new MemoryStream(new byte[StorageLimits.MaxFileBytes]), "application/zip", CancellationToken.None);

        Assert.Equal(StorageLimits.MaxFileBytes, size);
    }

    [Fact]
    public async Task Open_OfAMissingKey_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.OpenReadAsync(Key, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesTheBytesAndTheRecord_AndCanBeRepeated()
    {
        await _service.SaveAsync(Key, new MemoryStream([1]), "application/zip", CancellationToken.None);

        await _service.DeleteAsync(Key, CancellationToken.None);
        await _service.DeleteAsync(Key, CancellationToken.None);

        Assert.Empty(_blobs.Objects);
        Assert.Empty(_files.Items);
    }

    [Fact]
    public async Task Delete_WhenTheStoreFails_Throws_AndKeepsTheRecord()
    {
        await _service.SaveAsync(Key, new MemoryStream([1]), "application/zip", CancellationToken.None);
        _blobs.FailDeletes = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(Key, CancellationToken.None));

        Assert.Single(_files.Items);
    }

    private sealed class FakeBlobStore : IBlobStore
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public bool FailDeletes { get; set; }

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Objects[key] = copy.ToArray();
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Objects.TryGetValue(key, out var bytes)
                ? Task.FromResult<Stream>(new MemoryStream(bytes))
                : throw new NotFoundException("missing");

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            if (FailDeletes)
            {
                throw new InvalidOperationException("The object store is down.");
            }

            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStoredFileRepository : IStoredFileRepository
    {
        public List<StoredFile> Items { get; } = [];

        public Task<StoredFile?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(f => f.Key == key));

        public void Add(StoredFile file) => Items.Add(file);

        public void Remove(StoredFile file) => Items.Remove(file);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
