using System.Text.RegularExpressions;
using Partnr.Domain.Enums;
using Partnr.Domain.Exceptions;

namespace Partnr.Domain.Entities;

/// <summary>
/// Something that happened that one account should know about. The names are a snapshot of the moment, so the text
/// stays right if a project is renamed later; the ids say where to link. The email is delivered separately, with retries.
/// </summary>
public sealed partial class Notification : Entity
{
    public const int NameMaxLength = 200;
    public const int ErrorMaxLength = 500;
    public const int MaxEmailAttempts = 5;

    // The wait after the 1st, 2nd, 3rd and 4th failed attempt; the 5th failure is final.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    private Notification()
    {
    }

    private Notification(Guid recipientUserId, Guid workspaceId, string workspaceName, NotificationType type, DateTime createdAtUtc)
    {
        if (recipientUserId == Guid.Empty || workspaceId == Guid.Empty)
        {
            throw new DomainException("A notification is for an account, inside a workspace.");
        }

        RecipientUserId = recipientUserId;
        WorkspaceId = workspaceId;
        WorkspaceName = Snapshot(workspaceName) ?? throw new DomainException("A notification needs the workspace's name.");
        Type = type;
        CreatedAtUtc = createdAtUtc;
        EmailStatus = NotificationEmailStatus.Pending;
        EmailNextAttemptAtUtc = createdAtUtc;
    }

    public Guid RecipientUserId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? ClientId { get; private set; }

    /// <summary>The round this is about. Lets a withdrawn request cancel the email that has not left yet.</summary>
    public Guid? ApprovalRequestId { get; private set; }

    public string WorkspaceName { get; private set; } = string.Empty;

    public string? ProjectName { get; private set; }

    public string? DeliverableTitle { get; private set; }

    /// <summary>Who did it, when it was not the recipient (the client who answered, the client who joined).</summary>
    public string? ActorName { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    /// <summary>When what it says stopped being true (the request was taken back). It leaves the inbox and its email is cancelled.</summary>
    public DateTime? ObsoleteAtUtc { get; private set; }

    public NotificationEmailStatus EmailStatus { get; private set; }

    public int EmailAttempts { get; private set; }

    /// <summary>When the worker may pick it up next. A worker that claims a row pushes this forward, so two workers never take the same one.</summary>
    public DateTime EmailNextAttemptAtUtc { get; private set; }

    public DateTime? EmailSentAtUtc { get; private set; }

    public string? EmailLastError { get; private set; }

    public static Notification ApprovalRequested(
        Guid recipientUserId,
        Guid workspaceId,
        string workspaceName,
        Guid projectId,
        string projectName,
        string deliverableTitle,
        Guid approvalRequestId) =>
        ForApproval(NotificationType.ApprovalRequested, recipientUserId, workspaceId, workspaceName, projectId, projectName, deliverableTitle, approvalRequestId, actorName: null);

    public static Notification ApprovalDecided(
        ApprovalStatus decision,
        Guid recipientUserId,
        Guid workspaceId,
        string workspaceName,
        Guid projectId,
        string projectName,
        string deliverableTitle,
        Guid approvalRequestId,
        string actorName)
    {
        var type = decision switch
        {
            ApprovalStatus.Approved => NotificationType.ApprovalApproved,
            ApprovalStatus.ChangesRequested => NotificationType.ApprovalChangesRequested,
            _ => throw new DomainException("Only a decision is worth telling the freelancer about.")
        };

        return ForApproval(type, recipientUserId, workspaceId, workspaceName, projectId, projectName, deliverableTitle, approvalRequestId, actorName);
    }

    public static Notification InvitationAccepted(
        Guid recipientUserId,
        Guid workspaceId,
        string workspaceName,
        Guid clientId,
        string actorName)
    {
        if (clientId == Guid.Empty)
        {
            throw new DomainException("An accepted invitation is about a client.");
        }

        return new Notification(recipientUserId, workspaceId, workspaceName, NotificationType.InvitationAccepted, DateTime.UtcNow)
        {
            ClientId = clientId,
            ActorName = Snapshot(actorName) ?? throw new DomainException("A notification needs to say who it is about.")
        };
    }

    /// <summary>Marking it twice keeps the first date.</summary>
    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;

    /// <summary>An email already sent stays as it was; the notification only leaves the inbox.</summary>
    public void MarkObsolete(DateTime nowUtc)
    {
        ObsoleteAtUtc ??= nowUtc;
        CancelEmail();
    }

    /// <summary>Only an email that has not gone out yet can be cancelled; one already sent stays as it was.</summary>
    public void CancelEmail()
    {
        if (EmailStatus == NotificationEmailStatus.Pending)
        {
            EmailStatus = NotificationEmailStatus.Cancelled;
        }
    }

    public void MarkEmailSent(DateTime nowUtc)
    {
        EmailStatus = NotificationEmailStatus.Sent;
        EmailAttempts++;
        EmailSentAtUtc = nowUtc;
        EmailLastError = null;
    }

    /// <summary>Counts the attempt, then either schedules the next one or gives up for good.</summary>
    public void RecordEmailFailure(DateTime nowUtc, string error)
    {
        EmailAttempts++;
        EmailLastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;

        if (EmailAttempts >= MaxEmailAttempts)
        {
            EmailStatus = NotificationEmailStatus.Failed;
            return;
        }

        EmailNextAttemptAtUtc = nowUtc + RetryDelays[EmailAttempts - 1];
    }

    private static Notification ForApproval(
        NotificationType type,
        Guid recipientUserId,
        Guid workspaceId,
        string workspaceName,
        Guid projectId,
        string projectName,
        string deliverableTitle,
        Guid approvalRequestId,
        string? actorName)
    {
        if (projectId == Guid.Empty || approvalRequestId == Guid.Empty)
        {
            throw new DomainException("An approval notification is about a round of a project.");
        }

        var notification = new Notification(recipientUserId, workspaceId, workspaceName, type, DateTime.UtcNow)
        {
            ProjectId = projectId,
            ApprovalRequestId = approvalRequestId,
            ProjectName = Snapshot(projectName) ?? throw new DomainException("A notification needs the project's name."),
            DeliverableTitle = Snapshot(deliverableTitle) ?? throw new DomainException("A notification needs the deliverable's title."),
        };

        if (type != NotificationType.ApprovalRequested)
        {
            notification.ActorName = Snapshot(actorName) ?? throw new DomainException("A notification needs to say who it is about.");
        }

        return notification;
    }

    // One line, at most NameMaxLength: the text ends up in an email subject.
    private static string? Snapshot(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var line = Whitespace().Replace(text, " ").Trim();

        return line.Length > NameMaxLength ? line[..NameMaxLength] : line;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
