using System.Security.Claims;
using Partnr.Workspaces.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace Partnr.Workspaces.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
