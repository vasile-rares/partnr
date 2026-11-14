using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Messages;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Messaging;
using Partnr.Domain.Entities;

namespace Partnr.Application.Services;

internal sealed class PortalMessageService(
    IProjectRepository projects,
    IClientRepository clients,
    IWorkspaceRepository workspaces,
    ConversationThreads threads,
    WorkspaceAccess access) : IPortalMessageService
{
    public async Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();

        return await threads.SummariesAsync(userId, await OpenConversationsAsync(userId, workspaceId, cancellationToken), forFirm: false, cancellationToken);
    }

    public async Task<UnreadMessagesResponse> UnreadCountAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();
        var open = await OpenConversationsAsync(userId, workspaceId, cancellationToken);

        return new UnreadMessagesResponse(await threads.UnreadTotalAsync(userId, open.Select(c => c.Project.Id).ToList(), cancellationToken));
    }

    public async Task<ConversationResponse> GetAsync(Guid workspaceId, Guid projectId, Guid? beforeId, CancellationToken cancellationToken) =>
        await threads.ThreadAsync(await DoorAsync(workspaceId, projectId, cancellationToken), beforeId, cancellationToken);

    public async Task<MessageResponse> SendAsync(Guid workspaceId, Guid projectId, SaveMessageRequest request, CancellationToken cancellationToken) =>
        await threads.SendAsync(await DoorAsync(workspaceId, projectId, cancellationToken), request.Body, cancellationToken);

    public async Task<MessageResponse> EditAsync(
        Guid workspaceId,
        Guid projectId,
        Guid messageId,
        SaveMessageRequest request,
        CancellationToken cancellationToken) =>
        await threads.EditAsync(await DoorAsync(workspaceId, projectId, cancellationToken), messageId, request.Body, cancellationToken);

    public async Task DeleteAsync(Guid workspaceId, Guid projectId, Guid messageId, CancellationToken cancellationToken) =>
        await threads.DeleteAsync(await DoorAsync(workspaceId, projectId, cancellationToken), messageId, cancellationToken);

    public async Task MarkReadAsync(Guid workspaceId, Guid projectId, MarkConversationReadRequest request, CancellationToken cancellationToken) =>
        await threads.MarkReadAsync(await DoorAsync(workspaceId, projectId, cancellationToken), request.MessageId, cancellationToken);

    // The same door as the portal's projects: shared with this account, in this firm.
    private async Task<IReadOnlyList<(Project Project, Client Client)>> OpenConversationsAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var shared = (await projects.ListSharedWithClientAsync(userId, cancellationToken)).Where(p => p.WorkspaceId == workspaceId).ToList();
        var cards = (await clients.ListByIdsAsync(workspaceId, shared.Select(p => p.ClientId).Distinct().ToList(), cancellationToken))
            .ToDictionary(c => c.Id);

        return shared
            .Select(p => (Project: p, Client: cards.GetValueOrDefault(p.ClientId)))
            .Where(c => ConversationThreads.IsOpen(c.Project, c.Client))
            .Select(c => (c.Project, c.Client!))
            .ToList();
    }

    private async Task<ConversationDoor> DoorAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();
        var project = await projects.GetSharedWithClientAsync(userId, projectId, cancellationToken);
        var workspace = project?.WorkspaceId == workspaceId ? await workspaces.GetByIdAsync(workspaceId, cancellationToken) : null;
        var client = project is null ? null : await clients.GetByIdAsync(workspaceId, project.ClientId, cancellationToken);

        if (project is null || workspace is null || !ConversationThreads.IsOpen(project, client))
        {
            throw new NotFoundException($"The conversation of project '{projectId}' was not found.");
        }

        return new ConversationDoor(workspace, project, client!, userId, ForFirm: false);
    }
}
