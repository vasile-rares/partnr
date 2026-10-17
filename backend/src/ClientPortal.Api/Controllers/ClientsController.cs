using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/clients")]
public sealed class ClientsController(
    IClientService service,
    IClientInvitationService invitations,
    IClientSuggestionService suggestions) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(Guid workspaceId, RegisterClientRequest request, CancellationToken cancellationToken)
    {
        var client = await service.RegisterAsync(workspaceId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, id = client.Id }, client);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid workspaceId, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, includeArchived, cancellationToken));

    // Called on every pause while typing, so it has its own rate limit (see Program.cs).
    [EnableRateLimiting(RateLimitPolicies.Suggestions)]
    [HttpGet("suggestions")]
    public async Task<IActionResult> Suggest(Guid workspaceId, [FromQuery] string? email, CancellationToken cancellationToken) =>
        Ok(await suggestions.SuggestAsync(workspaceId, email, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, Guid id, UpdateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workspaceId, id, request, cancellationToken));

    // POST is both "invite" and "resend": a new invitation replaces the old one.
    [HttpPost("{id:guid}/invitation")]
    public async Task<IActionResult> Invite(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await invitations.InviteAsync(workspaceId, id, cancellationToken));

    [HttpDelete("{id:guid}/invitation")]
    public async Task<IActionResult> CancelInvitation(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await invitations.CancelAsync(workspaceId, id, cancellationToken));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ArchiveAsync(workspaceId, id, cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RestoreAsync(workspaceId, id, cancellationToken));
}
