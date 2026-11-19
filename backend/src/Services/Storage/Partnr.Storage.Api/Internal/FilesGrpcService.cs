using Google.Protobuf;
using Grpc.Core;
using Partnr.Contracts.Storage;
using Partnr.Storage.Application.Exceptions;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Domain.Exceptions;

namespace Partnr.Storage.Api.Internal;

/// <summary>
/// What the other services ask of the storage. It is served only on the internal gRPC port, which the gateway does not route to and
/// which must not be reachable from outside: callers are services, so there is no session to check.
/// </summary>
internal sealed class FilesGrpcService(IFileStorageService storage) : Files.FilesBase
{
    private const int ChunkSize = 64 * 1024;

    public override async Task<SaveReply> Save(IAsyncStreamReader<SaveRequest> requestStream, ServerCallContext context)
    {
        if (!await requestStream.MoveNext(context.CancellationToken) || requestStream.Current.PartCase != SaveRequest.PartOneofCase.Header)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "The first message must be the header."));
        }

        var header = requestStream.Current.Header;

        try
        {
            var size = await storage.SaveAsync(header.Key, new ChunkedRequestStream(requestStream), header.ContentType, context.CancellationToken);

            return new SaveReply { SizeBytes = size };
        }
        catch (Exception ex) when (ex is ValidationFailedException or DomainException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task Read(ReadRequest request, IServerStreamWriter<ReadReply> responseStream, ServerCallContext context)
    {
        Stream content;
        try
        {
            content = await storage.OpenReadAsync(request.Key, context.CancellationToken);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }

        await using (content)
        {
            var buffer = new byte[ChunkSize];
            int read;
            while ((read = await content.ReadAsync(buffer, context.CancellationToken)) > 0)
            {
                await responseStream.WriteAsync(new ReadReply { Chunk = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, read)) }, context.CancellationToken);
            }
        }
    }

    public override async Task<DeleteReply> Delete(DeleteRequest request, ServerCallContext context)
    {
        await storage.DeleteAsync(request.Key, context.CancellationToken);

        return new DeleteReply();
    }
}
