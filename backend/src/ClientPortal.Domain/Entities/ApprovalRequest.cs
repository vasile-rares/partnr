using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>One round of asking the client to sign off a deliverable. Rounds are kept, so the history of approvals is the list of them.</summary>
public sealed class ApprovalRequest : Entity
{
    public const int NoteMaxLength = 2000;
    public const int MessageMaxLength = 2000;

    private ApprovalRequest()
    {
    }

    private ApprovalRequest(Guid workspaceId, Guid projectId, Guid deliverableId, Guid requestedByUserId, string? note, DateTime requestedAtUtc)
    {
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        DeliverableId = deliverableId;
        RequestedByUserId = requestedByUserId;
        Note = Clean(note);
        RequestedAtUtc = requestedAtUtc;
        Status = ApprovalStatus.Pending;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid DeliverableId { get; private set; }

    public ApprovalStatus Status { get; private set; }

    /// <summary>What the freelancer asks the client to look at.</summary>
    public string? Note { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    /// <summary>Only set when the client decided, never for a withdrawal.</summary>
    public Guid? DecidedByUserId { get; private set; }

    /// <summary>The client's words: optional on approval, required when asking for changes.</summary>
    public string? Message { get; private set; }

    /// <summary>When the round closed, by a decision or a withdrawal.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsPending => Status == ApprovalStatus.Pending;

    public static ApprovalRequest Request(Guid workspaceId, Guid projectId, Guid deliverableId, Guid requestedByUserId, string? note)
    {
        if (workspaceId == Guid.Empty || projectId == Guid.Empty || deliverableId == Guid.Empty)
        {
            throw new DomainException("An approval request is for a deliverable of a project.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new DomainException("An approval request needs to know who asked.");
        }

        return new ApprovalRequest(workspaceId, projectId, deliverableId, requestedByUserId, note, DateTime.UtcNow);
    }

    public void Approve(Guid decidedByUserId, string? message) => Decide(ApprovalStatus.Approved, decidedByUserId, message);

    public void RequestChanges(Guid decidedByUserId, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Say what should change.");
        }

        Decide(ApprovalStatus.ChangesRequested, decidedByUserId, message);
    }

    public void Withdraw()
    {
        EnsurePending();

        Status = ApprovalStatus.Withdrawn;
        ResolvedAtUtc = DateTime.UtcNow;
    }

    private void Decide(ApprovalStatus status, Guid decidedByUserId, string? message)
    {
        EnsurePending();

        if (decidedByUserId == Guid.Empty)
        {
            throw new DomainException("A decision needs to know who made it.");
        }

        Status = status;
        DecidedByUserId = decidedByUserId;
        Message = Clean(message);
        ResolvedAtUtc = DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (!IsPending)
        {
            throw new DomainException("This approval request is already closed.");
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
