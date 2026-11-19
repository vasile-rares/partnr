using System.Security.Cryptography;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Storage;
using Partnr.Storage.Application;
using Partnr.Storage.Infrastructure.Persistence;

namespace Partnr.Storage.IntegrationTests;

/// <summary>What the other services ask of the storage, asked the way they ask it (over gRPC, in chunks, no session).</summary>
[Collection(ApiCollection.Name)]
public sealed class FilesGrpcTests(StorageFactory factory)
{
    [Fact]
    public async Task SaveThenRead_ReturnsTheSameBytes_AndKeepsWhatIsKnownAboutThem()
    {
        var key = NewKey();
        var bytes = RandomBytes(200_000); // several chunks

        var saved = await Save(key, bytes, "application/pdf");
        var read = await Read(key);

        Assert.Equal(bytes.Length, saved.SizeBytes);
        Assert.Equal(bytes, read);
        var record = await RecordOf(key);
        Assert.Equal(bytes.Length, record.SizeBytes);
        Assert.Equal("application/pdf", record.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), record.Sha256);
    }

    [Fact]
    public async Task SavingUnderAKeyAgain_ReplacesTheBytesAndTheRecord()
    {
        var key = NewKey();
        await Save(key, [1, 2, 3], "application/zip");

        await Save(key, [9, 9], "text/plain");

        Assert.Equal<byte[]>([9, 9], await Read(key));
        var record = await RecordOf(key);
        Assert.Equal(2, record.SizeBytes);
        Assert.Equal("text/plain", record.ContentType);
    }

    [Fact]
    public async Task AnEmptyFile_ReadsBackEmpty()
    {
        var key = NewKey();
        await Save(key, [], "text/plain");

        Assert.Empty(await Read(key));
    }

    [Fact]
    public async Task ReadingAMissingKey_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<RpcException>(() => Read(NewKey()));

        Assert.Equal(StatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task AFileOverTheLimit_IsRefused_AndNothingIsKept()
    {
        var key = NewKey();

        var error = await Assert.ThrowsAsync<RpcException>(() => Save(key, new byte[StorageLimits.MaxFileBytes + 1], "application/zip"));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
        Assert.False(factory.Blobs.Contains(key));
    }

    [Fact]
    public async Task AnUploadThatDoesNotStartWithAHeader_IsRefused()
    {
        using var channel = Channel();
        using var call = new Files.FilesClient(channel).Save();
        await call.RequestStream.WriteAsync(new SaveRequest { Chunk = Google.Protobuf.ByteString.CopyFrom([1]) });
        await call.RequestStream.CompleteAsync();

        var error = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheBytesAndTheRecord_AndCanBeRepeated()
    {
        var key = NewKey();
        await Save(key, [1, 2, 3], "application/zip");

        using var channel = Channel();
        var client = new Files.FilesClient(channel);
        await client.DeleteAsync(new DeleteRequest { Key = key });
        await client.DeleteAsync(new DeleteRequest { Key = key });

        Assert.False(factory.Blobs.Contains(key));
        Assert.Null(await FindRecord(key));
    }

    private static string NewKey() => $"{Guid.NewGuid()}/{Guid.NewGuid()}/{Guid.NewGuid()}";

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);

        return bytes;
    }

    private async Task<SaveReply> Save(string key, byte[] bytes, string contentType)
    {
        using var channel = Channel();
        using var call = new Files.FilesClient(channel).Save();
        await call.RequestStream.WriteAsync(new SaveRequest { Header = new SaveHeader { Key = key, ContentType = contentType } });
        for (var offset = 0; offset < bytes.Length; offset += 64 * 1024)
        {
            var count = Math.Min(64 * 1024, bytes.Length - offset);
            await call.RequestStream.WriteAsync(new SaveRequest { Chunk = Google.Protobuf.ByteString.CopyFrom(bytes, offset, count) });
        }

        await call.RequestStream.CompleteAsync();

        return await call.ResponseAsync;
    }

    private async Task<byte[]> Read(string key)
    {
        using var channel = Channel();
        using var call = new Files.FilesClient(channel).Read(new ReadRequest { Key = key });
        using var copy = new MemoryStream();
        while (await call.ResponseStream.MoveNext(CancellationToken.None))
        {
            call.ResponseStream.Current.Chunk.WriteTo(copy);
        }

        return copy.ToArray();
    }

    private async Task<Partnr.Storage.Domain.Entities.StoredFile> RecordOf(string key) =>
        await FindRecord(key) ?? throw new InvalidOperationException($"No record for {key}.");

    private async Task<Partnr.Storage.Domain.Entities.StoredFile?> FindRecord(string key)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<StorageDbContext>().StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Key == key);
    }

    private GrpcChannel Channel()
    {
        var http = factory.CreateDefaultClient(new ResponseVersionHandler());

        return GrpcChannel.ForAddress(http.BaseAddress!, new GrpcChannelOptions { HttpClient = http });
    }

    // The test server answers with HTTP/1.1 as the version, which a gRPC client refuses.
    private sealed class ResponseVersionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.Version = request.Version;
            return response;
        }
    }
}
