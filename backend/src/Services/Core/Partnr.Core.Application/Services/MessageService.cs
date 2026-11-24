using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs.Messages;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Application.Messaging;
using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Services;

internal sealed class MessageService(
    IProjectRepository projects,
    IClientRepository clients,
    ConversationThreads threads,
    ClientAccess clientAccess,
    CallerAccess callers) : IMessageService
{
    public async Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);

        return await threads.SummariesAsync(caller.UserId, await OpenConversationsAsync(caller, cancellationToken), forFirm: !caller.IsClient, cancellationToken);
    }

    public async Task<UnreadMessagesResponse> UnreadCountAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        var open = await OpenConversationsAsync(caller, cancellationToken);

        return new UnreadMessagesResponse(await threads.UnreadTotalAsync(caller.UserId, open.Select(c => c.Project.Id).ToList(), cancellationToken));
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

    // A member reaches every project of the workspace, a client only the ones shared with them; both only while the client can open it.
    private async Task<IReadOnlyList<(Project Project, Client Client)>> OpenConversationsAsync(Caller caller, CancellationToken cancellationToken)
    {
        var workspaceId = caller.Workspace.Id;
        var list = caller.IsClient
            ? await clientAccess.ListProjectsAsync(caller.UserId, workspaceId, cancellationToken)
            : await projects.ListAsync(workspaceId, includeArchived: false, clientId: null, cancellationToken);
        var cards = (await clients.ListByIdsAsync(workspaceId, list.Select(p => p.ClientId).Distinct().ToList(), cancellationToken))
            .ToDictionary(c => c.Id);

        return list
            .Select(p => (Project: p, Client: cards.GetValueOrDefault(p.ClientId)))
            .Where(c => c.Project.IsOpenToClient(c.Client))
            .Select(c => (c.Project, c.Client!))
            .ToList();
    }

    private async Task<ConversationDoor> DoorAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        var project = caller.IsClient
            ? await clientAccess.FindProjectInWorkspaceAsync(caller.UserId, workspaceId, projectId, cancellationToken)
            : await projects.GetByIdAsync(workspaceId, projectId, cancellationToken);
        var client = project is null ? null : await clients.GetByIdAsync(workspaceId, project.ClientId, cancellationToken);

        // A project the client cannot open has no conversation, so it looks the same as an unknown one.
        if (project is null || !project.IsOpenToClient(client))
        {
            throw new NotFoundException($"The conversation of project '{projectId}' was not found.");
        }

        return new ConversationDoor(caller.Workspace, project, client, caller.UserId, ForFirm: !caller.IsClient);
    }
}
