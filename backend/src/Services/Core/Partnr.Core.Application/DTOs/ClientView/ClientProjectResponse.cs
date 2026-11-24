using Partnr.Core.Application.DTOs.Projects;

namespace Partnr.Core.Application.DTOs.ClientView;

// Separate from ProjectResponse on purpose, so a workspace-side field never reaches a client by accident.

/// <param name="Status">Never Planning: a project in Planning is not shared yet.</param>
public sealed record ClientProjectSummaryResponse(
    Guid Id,
    ClientWorkspaceResponse Workspace,
    string Name,
    string Status,
    DateOnly? DueDate,
    ProjectProgressResponse Progress,
    int AwaitingApproval);

public sealed record ClientProjectResponse(
    Guid Id,
    ClientWorkspaceResponse Workspace,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    ProjectProgressResponse Progress,
    IReadOnlyList<StageResponse> Stages,
    IReadOnlyList<ClientDeliverableResponse> Deliverables,
    IReadOnlyList<ClientProjectUpdateResponse> Updates,
    IReadOnlyList<ClientProjectFileResponse> Files,
    int AwaitingApproval);

/// <summary>Only what was shared: no status (always Shared), no archive date, no ids of the workspace's side.</summary>
public sealed record ClientDeliverableResponse(
    Guid Id,
    string Title,
    string? Description,
    string Url,
    string? StageName,
    ClientApprovalResponse Approval);

/// <param name="Status">None, Pending, Approved or ChangesRequested: the latest round that was not withdrawn.</param>
/// <param name="Pending">The round waiting for the client's decision, if any.</param>
/// <param name="History">Decided rounds, newest first. Withdrawn rounds never show.</param>
public sealed record ClientApprovalResponse(
    string Status,
    ClientPendingApprovalResponse? Pending,
    IReadOnlyList<ClientApprovalDecisionResponse> History);

public sealed record ClientPendingApprovalResponse(Guid Id, string? Note, DateTime RequestedAtUtc);

/// <param name="Decision">Approved or ChangesRequested. No ids and no account: who answered is the client.</param>
public sealed record ClientApprovalDecisionResponse(
    string Decision,
    string? Note,
    string? Message,
    DateTime RequestedAtUtc,
    DateTime DecidedAtUtc);

/// <summary>Only what was shared, most recently shared first: no status, no author, no creation or archive date, no ids of the workspace's side.</summary>
public sealed record ClientProjectUpdateResponse(Guid Id, string Title, string? Body, DateTime SharedAtUtc);

/// <summary>Only what was shared, newest first: no status, no content type, no creation or archive date, no ids of the workspace's side.</summary>
public sealed record ClientProjectFileResponse(Guid Id, string Name, string? Description, string FileName, long SizeBytes);
