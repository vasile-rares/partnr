namespace ClientPortal.Application.DTOs.Approvals;

/// <summary>One round of asking the client to sign off a deliverable, as the workspace sees it (withdrawn rounds included).</summary>
public sealed record ApprovalResponse(
    Guid Id,
    string Status,
    string? Note,
    DateTime RequestedAtUtc,
    string? Message,
    DateTime? ResolvedAtUtc);
