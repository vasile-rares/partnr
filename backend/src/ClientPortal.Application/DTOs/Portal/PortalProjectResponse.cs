using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.Application.DTOs.Portal;

// Separate from ProjectResponse on purpose, so a workspace-side field never reaches the portal by accident.

/// <param name="Status">Never Planning: a project in Planning is not shared yet.</param>
public sealed record PortalProjectSummaryResponse(
    Guid Id,
    PortalWorkspaceResponse Workspace,
    string Name,
    string Status,
    DateOnly? DueDate,
    ProjectProgressResponse Progress,
    int AwaitingApproval);

public sealed record PortalProjectResponse(
    Guid Id,
    PortalWorkspaceResponse Workspace,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    ProjectProgressResponse Progress,
    IReadOnlyList<StageResponse> Stages,
    IReadOnlyList<PortalDeliverableResponse> Deliverables,
    IReadOnlyList<PortalProjectUpdateResponse> Updates,
    int AwaitingApproval);

/// <summary>Only what was shared: no status (always Shared), no archive date, no ids of the workspace's side.</summary>
public sealed record PortalDeliverableResponse(
    Guid Id,
    string Title,
    string? Description,
    string Url,
    string? StageName,
    PortalApprovalResponse Approval);

/// <param name="Status">None, Pending, Approved or ChangesRequested: the latest round that was not withdrawn.</param>
/// <param name="Pending">The round waiting for the client's decision, if any.</param>
/// <param name="History">Decided rounds, newest first. Withdrawn rounds never show.</param>
public sealed record PortalApprovalResponse(
    string Status,
    PortalPendingApprovalResponse? Pending,
    IReadOnlyList<PortalApprovalDecisionResponse> History);

public sealed record PortalPendingApprovalResponse(Guid Id, string? Note, DateTime RequestedAtUtc);

/// <param name="Decision">Approved or ChangesRequested. No ids and no account: who answered is the client.</param>
public sealed record PortalApprovalDecisionResponse(
    string Decision,
    string? Note,
    string? Message,
    DateTime RequestedAtUtc,
    DateTime DecidedAtUtc);

/// <summary>Only what was shared, most recently shared first: no status, no author, no creation or archive date, no ids of the workspace's side.</summary>
public sealed record PortalProjectUpdateResponse(Guid Id, string Title, string? Body, DateTime SharedAtUtc);
