using Grpc.Core;
using Partnr.Contracts.Storage;

namespace Partnr.Storage.Api.Internal;

/// <summary>The chunks of an upload (after its header) as one readable stream.</summary>
internal sealed class ChunkedRequestStream(IAsyncStreamReader<SaveRequest> chunks) : Stream
{
    private ReadOnlyMemory<byte> _current;

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
            if (!await chunks.MoveNext(cancellationToken))
            {
                return 0;
            }

            if (chunks.Current.PartCase != SaveRequest.PartOneofCase.Chunk)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "After the header only chunks may follow."));
            }

            _current = chunks.Current.Chunk.Memory;
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
}
