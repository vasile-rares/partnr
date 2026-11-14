using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Messages;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Messaging;
using Partnr.Domain.Entities;

namespace Partnr.Application.Services;

internal sealed class MessageService(
    IProjectRepository projects,
    IClientRepository clients,
    ConversationThreads threads,
    WorkspaceAccess access) : IMessageService
{
    public async Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);

        return await threads.SummariesAsync(membership.UserId, await OpenConversationsAsync(workspaceId, cancellationToken), forFirm: true, cancellationToken);
    }

    public async Task<UnreadMessagesResponse> UnreadCountAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var open = await OpenConversationsAsync(workspaceId, cancellationToken);

        return new UnreadMessagesResponse(await threads.UnreadTotalAsync(membership.UserId, open.Select(c => c.Project.Id).ToList(), cancellationToken));
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

    private async Task<IReadOnlyList<(Project Project, Client Client)>> OpenConversationsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var list = await projects.ListAsync(workspaceId, includeArchived: false, clientId: null, cancellationToken);
        var cards = (await clients.ListByIdsAsync(workspaceId, list.Select(p => p.ClientId).Distinct().ToList(), cancellationToken))
            .ToDictionary(c => c.Id);

        return list
            .Select(p => (Project: p, Client: cards.GetValueOrDefault(p.ClientId)))
            .Where(c => ConversationThreads.IsOpen(c.Project, c.Client))
            .Select(c => (c.Project, c.Client!))
            .ToList();
    }

    private async Task<ConversationDoor> DoorAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projects.GetByIdAsync(workspaceId, projectId, cancellationToken);
        var client = project is null ? null : await clients.GetByIdAsync(workspaceId, project.ClientId, cancellationToken);

        // A project the client cannot open has no conversation, so it looks the same as an unknown one.
        if (project is null || !ConversationThreads.IsOpen(project, client))
        {
            throw new NotFoundException($"The conversation of project '{projectId}' was not found.");
        }

        return new ConversationDoor(membership.Workspace, project, client!, membership.UserId, ForFirm: true);
    }
}
