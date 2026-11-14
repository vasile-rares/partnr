using Partnr.Domain.Exceptions;

namespace Partnr.Domain.Entities;

/// <summary>How far one person has read a project's conversation. Everything newer that someone else wrote is unread for them.</summary>
public sealed class ConversationRead : Entity
{
    private ConversationRead()
    {
    }

    private ConversationRead(Guid userId, Guid projectId, DateTime lastReadAtUtc)
    {
        UserId = userId;
        ProjectId = projectId;
        LastReadAtUtc = lastReadAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The time of the newest message this person has seen.</summary>
    public DateTime LastReadAtUtc { get; private set; }

    public static ConversationRead Start(Guid userId, Guid projectId, DateTime readUpToUtc)
    {
        if (userId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainException("A read mark is for one person in one conversation.");
        }

        return new ConversationRead(userId, projectId, readUpToUtc);
    }

    /// <summary>Never moves back: an older tab marking what it saw cannot make newer messages unread again.</summary>
    public void ReadUpTo(DateTime readUpToUtc)
    {
        if (readUpToUtc > LastReadAtUtc)
        {
            LastReadAtUtc = readUpToUtc;
        }
    }
}
