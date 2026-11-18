using System.ComponentModel.DataAnnotations;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.DTOs.Messages;

/// <param name="ClientName">Only on the firm's side; the client knows who they are.</param>
public sealed record ConversationSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string? ClientName,
    MessagePreviewResponse? LastMessage,
    int Unread);

/// <param name="Text">Null when that message was deleted.</param>
public sealed record MessagePreviewResponse(string AuthorName, string? Text, DateTime CreatedAtUtc);

public sealed record ConversationResponse(
    Guid ProjectId,
    string ProjectName,
    string? ClientName,
    IReadOnlyList<MessageResponse> Messages,
    bool HasEarlier);

/// <param name="Body">Null when deleted.</param>
/// <param name="FromClient">Written by the client, not by someone of the firm. No account ids reach the browser.</param>
/// <param name="ChangeableUntilUtc">Set only for the reader's own message while it can still be edited or deleted.</param>
public sealed record MessageResponse(
    Guid Id,
    string AuthorName,
    bool IsMine,
    bool FromClient,
    string? Body,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    bool IsDeleted,
    DateTime? ChangeableUntilUtc);

public sealed record UnreadMessagesResponse(int Unread);

public sealed record SaveMessageRequest
{
    [Required]
    [MaxLength(Message.BodyMaxLength)]
    public string Body { get; init; } = string.Empty;
}

/// <summary>Read up to this message: the newest one the page shows, so a message that arrived meanwhile stays unread.</summary>
public sealed record MarkConversationReadRequest
{
    [Required]
    public Guid MessageId { get; init; }
}
