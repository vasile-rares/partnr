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
    ProjectProgressResponse Progress);

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
    IReadOnlyList<PortalDeliverableResponse> Deliverables);

/// <summary>Only what was shared: no status (always Shared), no archive date, no ids of the workspace's side.</summary>
public sealed record PortalDeliverableResponse(Guid Id, string Title, string? Description, string Url, string? StageName);
