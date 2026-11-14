using Partnr.Application.DTOs.Auth;
using Partnr.Application.DTOs.Messages;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Messaging;

/// <summary>A conversation already found through the firm's or the client's door: who is reading, and where.</summary>
internal sealed record ConversationDoor(Workspace Workspace, Project Project, Client Client, Guid UserId, bool ForFirm)
{
    /// <summary>Everyone who can read this conversation: the workspace's members and the client's account.</summary>
    public IReadOnlyCollection<Guid> Readers =>
        Workspace.Members.Select(m => m.UserId).Append(Client.UserId!.Value).Distinct().ToList();
}

/// <summary>What both doors share once the conversation is found: listing, reading, writing, changing and marking read.</summary>
internal sealed class ConversationThreads(
    IMessageRepository messages,
    IConversationReadRepository reads,
    IIdentityService identity,
    IConversationPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const int PageSize = 50;

    private const string FormerMember = "Former member";

    /// <summary>
    /// The one rule, shared with the portal: a conversation exists while the client can open the project. In Planning, archived,
    /// or with a client who has no account (or an archived card) there is nobody on the other side.
    /// </summary>
    public static bool IsOpen(Project project, Client? client) =>
        !project.IsArchived
        && project.Status != ProjectStatus.Planning
        && client is { UserId: not null, IsArchived: false };

    public async Task<IReadOnlyList<ConversationSummaryResponse>> SummariesAsync(
        Guid userId,
        IReadOnlyList<(Project Project, Client Client)> conversations,
        bool forFirm,
        CancellationToken cancellationToken)
    {
        if (conversations.Count == 0)
        {
            return [];
        }

        var projectIds = conversations.Select(c => c.Project.Id).ToList();
        var latest = await messages.GetLatestPerProjectAsync(projectIds, cancellationToken);
        var unread = await messages.CountUnreadPerProjectAsync(userId, projectIds, cancellationToken);
        var names = await NamesAsync(
            latest.Values.Select(m => m.AuthorUserId).Concat(forFirm ? conversations.Select(c => c.Client.UserId!.Value) : []),
            cancellationToken);

        return conversations
            .Select(c =>
            {
                var last = latest.GetValueOrDefault(c.Project.Id);
                return new ConversationSummaryResponse(
                    c.Project.Id,
                    c.Project.Name,
                    forFirm ? ClientName(c.Client, names) : null,
                    last is null ? null : new MessagePreviewResponse(NameOf(last.AuthorUserId, names), last.IsDeleted ? null : last.Body, last.CreatedAtUtc),
                    unread.GetValueOrDefault(c.Project.Id));
            })
            // The latest conversation first; the ones nobody has written in yet after them, by name.
            .OrderByDescending(s => s.LastMessage?.CreatedAtUtc ?? DateTime.MinValue)
            .ThenBy(s => s.ProjectName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<int> UnreadTotalAsync(Guid userId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        projectIds.Count == 0 ? 0 : (await messages.CountUnreadPerProjectAsync(userId, projectIds, cancellationToken)).Values.Sum();

    public async Task<ConversationResponse> ThreadAsync(ConversationDoor door, Guid? beforeId, CancellationToken cancellationToken)
    {
        var page = await messages.ListPageAsync(door.Project.Id, beforeId, PageSize, cancellationToken);
        var names = await NamesAsync(
            page.Messages.Select(m => m.AuthorUserId).Append(door.Client.UserId!.Value),
            cancellationToken);
        var now = Now();

        return new ConversationResponse(
            door.Project.Id,
            door.Project.Name,
            door.ForFirm ? ClientName(door.Client, names) : null,
            page.Messages.Select(m => ToResponse(m, door, names, now)).ToList(),
            page.HasEarlier);
    }

    public async Task<MessageResponse> SendAsync(ConversationDoor door, string body, CancellationToken cancellationToken)
    {
        var message = Message.Write(door.Workspace.Id, door.Project.Id, door.UserId, body, Now());
        messages.Add(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.ConversationChangedAsync(door.Readers, door.Project.Id, cancellationToken);

        return await RespondAsync(message, door, cancellationToken);
    }

    public async Task<MessageResponse> EditAsync(ConversationDoor door, Guid messageId, string body, CancellationToken cancellationToken)
    {
        var message = await RequireChangeableAsync(door, messageId, cancellationToken);
        message.Edit(door.UserId, body, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.ConversationChangedAsync(door.Readers, door.Project.Id, cancellationToken);

        return await RespondAsync(message, door, cancellationToken);
    }

    public async Task DeleteAsync(ConversationDoor door, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await RequireChangeableAsync(door, messageId, cancellationToken);
        message.Delete(door.UserId, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.ConversationChangedAsync(door.Readers, door.Project.Id, cancellationToken);
    }

    public async Task MarkReadAsync(ConversationDoor door, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await messages.GetByIdAsync(door.Project.Id, messageId, cancellationToken)
            ?? throw new NotFoundException($"The message with id '{messageId}' was not found.");

        var read = await reads.GetAsync(door.UserId, door.Project.Id, cancellationToken);
        if (read is null)
        {
            reads.Add(ConversationRead.Start(door.UserId, door.Project.Id, message.CreatedAtUtc));
        }
        else
        {
            read.ReadUpTo(message.CreatedAtUtc);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Only this person's count changed; their other tabs follow.
        await publisher.ConversationChangedAsync([door.UserId], door.Project.Id, cancellationToken);
    }

    private async Task<Message> RequireChangeableAsync(ConversationDoor door, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await messages.GetByIdAsync(door.Project.Id, messageId, cancellationToken)
            ?? throw new NotFoundException($"The message with id '{messageId}' was not found.");

        if (message.AuthorUserId != door.UserId)
        {
            throw new ForbiddenException("Only the author can change a message.");
        }

        if (message.IsDeleted)
        {
            throw new ConflictException("This message was deleted.");
        }

        if (!message.CanBeChangedBy(door.UserId, Now()))
        {
            throw new ConflictException("A message can only be changed in its first 10 minutes.");
        }

        return message;
    }

    private async Task<MessageResponse> RespondAsync(Message message, ConversationDoor door, CancellationToken cancellationToken)
    {
        var names = await NamesAsync([message.AuthorUserId], cancellationToken);
        return ToResponse(message, door, names, Now());
    }

    private static MessageResponse ToResponse(Message m, ConversationDoor door, IReadOnlyDictionary<Guid, CurrentUserResponse> names, DateTime now) =>
        new(
            m.Id,
            NameOf(m.AuthorUserId, names),
            m.AuthorUserId == door.UserId,
            m.AuthorUserId == door.Client.UserId,
            m.IsDeleted ? null : m.Body,
            m.CreatedAtUtc,
            m.EditedAtUtc,
            m.IsDeleted,
            m.CanBeChangedBy(door.UserId, now) ? m.CreatedAtUtc + Message.ChangeWindow : null);

    private async Task<IReadOnlyDictionary<Guid, CurrentUserResponse>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, CurrentUserResponse>()
            : (await identity.FindByIdsAsync(ids, cancellationToken)).ToDictionary(a => a.Id);
    }

    private static string NameOf(Guid userId, IReadOnlyDictionary<Guid, CurrentUserResponse> names) =>
        names.TryGetValue(userId, out var account)
            ? string.IsNullOrWhiteSpace(account.FullName) ? account.Email : account.FullName
            : FormerMember;

    // The account's name wins over the card's, as everywhere else.
    private static string ClientName(Client client, IReadOnlyDictionary<Guid, CurrentUserResponse> names) =>
        client.UserId is { } id && names.TryGetValue(id, out var account) && !string.IsNullOrWhiteSpace(account.FullName)
            ? account.FullName
            : client.Name ?? client.Email;

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
