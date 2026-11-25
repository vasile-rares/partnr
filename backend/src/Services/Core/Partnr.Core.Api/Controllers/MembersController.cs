using Partnr.Core.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Core.Api.Controllers;

/// <summary>The team: any member reads it, only the owner changes it.</summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/members")]
public sealed class MembersController(IMemberService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workspaceId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, cancellationToken));

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        await service.RemoveAsync(workspaceId, userId, cancellationToken);
        return NoContent();
    }
}
