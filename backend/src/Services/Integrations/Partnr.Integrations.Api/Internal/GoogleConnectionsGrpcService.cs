using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Partnr.Contracts.Integrations;
using Partnr.Integrations.Application.Interfaces.Services;

namespace Partnr.Integrations.Api.Internal;

/// <summary>
/// What other services ask about an account's Google connection while a person waits for the answer. It is served only on the
/// internal gRPC port, which the gateway does not route to and which must not be reachable from outside.
/// </summary>
// Open to the fallback policy on purpose: callers are services, which have no session.
[AllowAnonymous]
internal sealed class GoogleConnectionsGrpcService(IGoogleConnectionService connections) : GoogleConnections.GoogleConnectionsBase
{
    public override async Task<GetStatusReply> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id is not an account id."));
        }

        var status = await connections.StatusOfAsync(userId, context.CancellationToken);

        return new GetStatusReply
        {
            State = status switch
            {
                "Connected" => ConnectionState.Connected,
                "NeedsReconnect" => ConnectionState.NeedsReconnect,
                _ => ConnectionState.NotConnected,
            },
        };
    }
}
