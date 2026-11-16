using Partnr.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Api.Controllers;

/// <summary>The client's side of the same meetings, per firm, like the rest of the portal.</summary>
[ApiController]
[Route("api/portal/workspaces/{workspaceId:guid}/meetings")]
public sealed class PortalMeetingsController(IPortalMeetingService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workspaceId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, cancellationToken));
}
