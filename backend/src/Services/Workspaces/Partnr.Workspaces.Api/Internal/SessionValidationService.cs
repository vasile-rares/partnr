using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Partnr.Contracts.Sessions;

namespace Partnr.Workspaces.Api.Internal;

/// <summary>
/// The gateway's question "is this session cookie good?". The cookie arrives as a header of the call, so the same cookie
/// handler answers (account still there, security stamp unchanged) as on a direct request.
/// </summary>
// Open to the fallback policy on purpose: the caller has no proof yet, it is asking for it.
[AllowAnonymous]
internal sealed class SessionValidationService : SessionValidation.SessionValidationBase
{
    public override async Task<ValidateReply> Validate(ValidateRequest request, ServerCallContext context)
    {
        var result = await context.GetHttpContext().AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var userId = result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return result.Succeeded && userId is not null
            ? new ValidateReply { Valid = true, UserId = userId }
            : new ValidateReply { Valid = false };
    }
}
