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
            using var call = client.ValidateAsync(
                new ValidateRequest(), new Metadata { { "cookie", cookieHeader } }, DateTime.UtcNow.Add(Deadline), cancellationToken);

            var reply = await call.ResponseAsync;
            var headers = await call.ResponseHeadersAsync;
            var setCookies = headers.Where(entry => entry.Key == "set-cookie").Select(entry => entry.Value).ToList();

            return reply.Valid ? new SessionCheck(SessionOutcome.Valid, reply.UserId, setCookies) : new SessionCheck(SessionOutcome.Invalid, SetCookies: setCookies);
        }
        catch (RpcException)
        {
            return new SessionCheck(SessionOutcome.Unavailable);
        }
    }
}
