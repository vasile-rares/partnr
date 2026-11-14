using Partnr.Domain.Exceptions;

namespace Partnr.Domain.Entities;

/// <summary>
/// One message in a project's conversation between the firm and its client. Plain text. Its author may correct or delete it
/// for a short while; after that it stays as it is, because the conversation is the record of what was said.
/// </summary>
public sealed class Message : Entity
{
    public const int BodyMaxLength = 4000;

    public static readonly TimeSpan ChangeWindow = TimeSpan.FromMinutes(10);

    private Message()
    {
    }

    private Message(Guid workspaceId, Guid projectId, Guid authorUserId, string body, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    /// <summary>Empty once deleted: the words are gone, only the place in the conversation stays.</summary>
    public string Body { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

    public static Message Write(Guid workspaceId, Guid projectId, Guid authorUserId, string body, DateTime nowUtc)
    {
        if (workspaceId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("A message belongs to a project's conversation.");
        }

        if (authorUserId == Guid.Empty)
        {
            throw new DomainException("A message needs to know who wrote it.");
        }

        return new Message(workspaceId, projectId, authorUserId, Clean(body), nowUtc);
    }

    public bool CanBeChangedBy(Guid userId, DateTime nowUtc) =>
        userId == AuthorUserId && !IsDeleted && nowUtc < CreatedAtUtc + ChangeWindow;

    public void Edit(Guid userId, string body, DateTime nowUtc)
    {
        EnsureChangeable(userId, nowUtc);

        Body = Clean(body);
        EditedAtUtc = nowUtc;
    }

    public void Delete(Guid userId, DateTime nowUtc)
    {
        EnsureChangeable(userId, nowUtc);

        Body = string.Empty;
        DeletedAtUtc = nowUtc;
    }

    private void EnsureChangeable(Guid userId, DateTime nowUtc)
    {
        if (userId != AuthorUserId)
        {
            throw new DomainException("Only the author can change a message.");
        }

        if (IsDeleted)
        {
            throw new DomainException("This message was deleted.");
        }

        if (nowUtc >= CreatedAtUtc + ChangeWindow)
        {
            throw new DomainException("A message can only be changed in its first 10 minutes.");
        }
    }

    // Line breaks are kept: they are part of what was written. Only the ends are trimmed.
    private static string Clean(string? body)
    {
        var text = body?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new DomainException("A message needs some text.");
        }

        if (text.Length > BodyMaxLength)
        {
            throw new DomainException($"A message can be at most {BodyMaxLength} characters.");
        }

        return text;
    }
}
