using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Core.Api.Controllers;

/// <summary>Invitations to the team, all owner-only. Sending to an address that has an open one replaces it, which is how it is resent.</summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/member-invitations")]
public sealed class MemberInvitationsController(IMemberInvitationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workspaceId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Invite(Guid workspaceId, InviteMemberRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.InviteAsync(workspaceId, request, cancellationToken));

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Cancel(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken)
    {
        await service.CancelAsync(workspaceId, invitationId, cancellationToken);
        return NoContent();
    }
}
