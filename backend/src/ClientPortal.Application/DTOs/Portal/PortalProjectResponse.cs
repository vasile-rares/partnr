using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.Application.DTOs.Portal;

// What a client may see of a project: its progress and stages, never the workspace's own data (the client card, its notes,
// other clients). These are separate from ProjectResponse on purpose, so a field added there does not reach the portal by accident.

/// <summary>A project in the client's list.</summary>
/// <param name="Status">One of <c>ProjectStatus</c>, as text. Never Planning: a project in Planning is not shared yet.</param>
public sealed record PortalProjectSummaryResponse(
    Guid Id,
    PortalWorkspaceResponse Workspace,
    string Name,
    string Status,
    DateOnly? DueDate,
    ProjectProgressResponse Progress);

/// <summary>A project with everything the client sees on its page.</summary>
/// <param name="Stages">In order, first to last.</param>
public sealed record PortalProjectResponse(
    Guid Id,
    PortalWorkspaceResponse Workspace,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    ProjectProgressResponse Progress,
    IReadOnlyList<StageResponse> Stages);
