using Grpc.Core;
using Partnr.Contracts.Integrations;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.Infrastructure.Integrations;

/// <summary>Asks the Integrations service over gRPC. A person is waiting, so a short deadline and a clear 503 when it does not answer.</summary>
internal sealed class GrpcGoogleConnectionStatus(GoogleConnections.GoogleConnectionsClient client) : IGoogleConnectionStatus
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<bool> IsConnectedAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await client.GetStatusAsync(
                new GetStatusRequest { UserId = userId.ToString() }, deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: cancellationToken);

            return reply.State == ConnectionState.Connected;
        }
        catch (RpcException ex)
        {
            throw new ServiceUnavailableException("Google Calendar cannot be reached right now. Try again in a moment.", ex);
        }
    }
}
