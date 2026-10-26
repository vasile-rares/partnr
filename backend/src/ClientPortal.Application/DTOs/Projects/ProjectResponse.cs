namespace ClientPortal.Application.DTOs.Projects;

public sealed record ProjectResponse(
    Guid Id,
    ProjectClientResponse Client,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    ProjectProgressResponse Progress,
    ProjectApprovalsResponse Approvals,
    IReadOnlyList<StageResponse> Stages);

/// <param name="Pending">Deliverables waiting for the client's answer.</param>
/// <param name="ChangesRequested">Deliverables whose latest answer asked for changes and have not been asked about again.</param>
public sealed record ProjectApprovalsResponse(int Pending, int ChangesRequested);

/// <param name="DisplayName">The client's name, or their email while the card has no name (same rule as the client list).</param>
public sealed record ProjectClientResponse(Guid Id, string DisplayName);

public sealed record ProjectProgressResponse(int Done, int Total);

public sealed record StageResponse(Guid Id, string Name, string? Description, DateOnly? DueDate, string Status);
