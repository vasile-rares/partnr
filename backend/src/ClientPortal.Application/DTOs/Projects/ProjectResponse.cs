namespace ClientPortal.Application.DTOs.Projects;

/// <param name="Status">One of <c>ProjectStatus</c> (Planning, Active, OnHold, Completed), as text.</param>
/// <param name="Stages">In order, first to last.</param>
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

/// <summary>How many of the project's stages are done.</summary>
public sealed record ProjectProgressResponse(int Done, int Total);

/// <param name="Status">One of <c>StageStatus</c> (ToDo, InProgress, Done), as text.</param>
public sealed record StageResponse(Guid Id, string Name, string? Description, DateOnly? DueDate, string Status);
