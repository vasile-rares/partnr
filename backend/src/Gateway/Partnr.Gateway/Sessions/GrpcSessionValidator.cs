using Grpc.Core;
using Partnr.Contracts.Sessions;

namespace Partnr.Gateway.Sessions;

internal sealed class GrpcSessionValidator(SessionValidation.SessionValidationClient client) : ISessionValidator
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<SessionCheck> ValidateAsync(string cookieHeader, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await client.ValidateAsync(
                new ValidateRequest(), new Metadata { { "cookie", cookieHeader } }, DateTime.UtcNow.Add(Deadline), cancellationToken);

            return reply.Valid ? new SessionCheck(SessionOutcome.Valid, reply.UserId) : new SessionCheck(SessionOutcome.Invalid);
        }
        catch (RpcException)
        {
            return new SessionCheck(SessionOutcome.Unavailable);
        }
    }
}
