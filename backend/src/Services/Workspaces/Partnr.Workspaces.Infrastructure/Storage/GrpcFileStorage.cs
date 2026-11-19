using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Partnr.Contracts.Storage;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.Infrastructure.Storage;

/// <summary>Keeps the bytes in the Storage service, over gRPC, in chunks. A person is waiting, so there are deadlines and a clear 503.</summary>
internal sealed class GrpcFileStorage(Files.FilesClient client, ILogger<GrpcFileStorage> logger) : IFileStorage
{
    private const int ChunkSize = 64 * 1024;
    private static readonly TimeSpan TransferDeadline = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeleteDeadline = TimeSpan.FromSeconds(10);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.Save(deadline: DateTime.UtcNow.Add(TransferDeadline), cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(
                new SaveRequest { Header = new SaveHeader { Key = key, ContentType = contentType } },
                cancellationToken);

            var buffer = new byte[ChunkSize];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await call.RequestStream.WriteAsync(
                    new SaveRequest { Chunk = ByteString.CopyFrom(buffer, 0, read) },
                    cancellationToken);
            }

            await call.RequestStream.CompleteAsync();
            await call.ResponseAsync;
        }
        catch (RpcException ex)
        {
            throw Unavailable(ex);
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var call = client.Read(new ReadRequest { Key = key }, deadline: DateTime.UtcNow.Add(TransferDeadline), cancellationToken: cancellationToken);

        try
        {
            // The first chunk is awaited here, so a missing file is a 404 before the response starts.
            var first = await call.ResponseStream.MoveNext(cancellationToken)
                ? call.ResponseStream.Current.Chunk.Memory
                : ReadOnlyMemory<byte>.Empty;

            return new ReplyStream(call, first);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            call.Dispose();
            throw new NotFoundException("The file's content was not found.");
        }
        catch (RpcException ex)
        {
            call.Dispose();
            throw Unavailable(ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteAsync(new DeleteRequest { Key = key }, deadline: DateTime.UtcNow.Add(DeleteDeadline), cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Could not delete {Key} from the Storage service; it stays behind as an orphan.", key);
        }
    }

    private static ServiceUnavailableException Unavailable(RpcException ex) =>
        new("The file storage cannot be reached right now. Try again in a moment.", ex);

    /// <summary>The chunks of a download as one readable stream.</summary>
    private sealed class ReplyStream(AsyncServerStreamingCall<ReadReply> call, ReadOnlyMemory<byte> first) : Stream
    {
        private ReadOnlyMemory<byte> _current = first;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (_current.IsEmpty)
            {
                try
                {
                    if (!await call.ResponseStream.MoveNext(cancellationToken))
                    {
                        return 0;
                    }
                }
                catch (RpcException ex)
                {
                    throw Unavailable(ex);
                }

                _current = call.ResponseStream.Current.Chunk.Memory;
            }

            var count = Math.Min(buffer.Length, _current.Length);
            _current[..count].CopyTo(buffer);
            _current = _current[count..];

            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                call.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
