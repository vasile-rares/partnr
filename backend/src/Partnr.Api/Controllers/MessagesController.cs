using Partnr.Application.DTOs.Messages;
using Partnr.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Api.Controllers;

/// <summary>The firm's side of the project conversations; a conversation is named by its project.</summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/conversations")]
public sealed class MessagesController(IMessageService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workspaceId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(Guid workspaceId, CancellationToken cancellationToken) =>
        Ok(await service.UnreadCountAsync(workspaceId, cancellationToken));

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Get(Guid workspaceId, Guid projectId, [FromQuery] Guid? before, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(workspaceId, projectId, before, cancellationToken));

    [HttpPost("{projectId:guid}/messages")]
    public async Task<IActionResult> Send(Guid workspaceId, Guid projectId, SaveMessageRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.SendAsync(workspaceId, projectId, request, cancellationToken));

    [HttpPut("{projectId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> Edit(
        Guid workspaceId,
        Guid projectId,
        Guid messageId,
        SaveMessageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.EditAsync(workspaceId, projectId, messageId, request, cancellationToken));

    [HttpDelete("{projectId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid projectId, Guid messageId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(workspaceId, projectId, messageId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid workspaceId,
        Guid projectId,
        MarkConversationReadRequest request,
        CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(workspaceId, projectId, request, cancellationToken);
        return NoContent();
    }
}
