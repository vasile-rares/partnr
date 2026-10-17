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
    IReadOnlyList<StageResponse> Stages);

/// <param name="DisplayName">The client's name, or their email while the card has no name (same rule as the client list).</param>
public sealed record ProjectClientResponse(Guid Id, string DisplayName);

public sealed record ProjectProgressResponse(int Done, int Total);

public sealed record StageResponse(Guid Id, string Name, string? Description, DateOnly? DueDate, string Status);
