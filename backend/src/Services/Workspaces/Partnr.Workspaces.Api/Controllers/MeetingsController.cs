using Partnr.Workspaces.Application.DTOs.Meetings;
using Partnr.Workspaces.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Workspaces.Api.Controllers;

/// <summary>The meetings of a workspace. Only the list is open to a client (their own, in their own shape); the rest is for members.</summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/meetings")]
public sealed class MeetingsController(IMeetingService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid workspaceId, CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        var meeting = await service.CreateAsync(workspaceId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, id = meeting.Id }, meeting);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        Guid workspaceId,
        [FromQuery] bool includeCancelled,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken) =>
        Ok((await service.ListAsync(workspaceId, includeCancelled, clientId, projectId, cancellationToken)).Body);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, Guid id, SaveMeetingRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workspaceId, id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(workspaceId, id, cancellationToken));

    [HttpPost("{id:guid}/retry-sync")]
    public async Task<IActionResult> RetrySync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RetrySyncAsync(workspaceId, id, cancellationToken));
}
