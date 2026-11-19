using System.Security.Claims;
using Partnr.Notifications.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace Partnr.Notifications.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
