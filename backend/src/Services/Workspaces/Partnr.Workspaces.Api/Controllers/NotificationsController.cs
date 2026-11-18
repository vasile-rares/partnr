using Partnr.Workspaces.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Workspaces.Api.Controllers;

/// <summary>The signed-in account's own notifications, so no workspace in the route.</summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Ok(await service.UnreadCountAsync(cancellationToken));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await service.MarkAllReadAsync(cancellationToken);

        return NoContent();
    }
}
