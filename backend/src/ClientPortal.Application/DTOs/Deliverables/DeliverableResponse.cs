using ClientPortal.Application.DTOs.Approvals;

namespace ClientPortal.Application.DTOs.Deliverables;

/// <param name="ApprovalStatus">
/// None, Pending, Approved or ChangesRequested: the status of the latest round that was not withdrawn.
/// </param>
/// <param name="Approvals">Every round, newest first.</param>
public sealed record DeliverableResponse(
    Guid Id,
    Guid ProjectId,
    Guid? StageId,
    string? StageName,
    string Title,
    string? Description,
    string Url,
    string Status,
    string ApprovalStatus,
    IReadOnlyList<ApprovalResponse> Approvals,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);
