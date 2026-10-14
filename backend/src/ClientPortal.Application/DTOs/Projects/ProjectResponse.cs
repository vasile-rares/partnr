namespace ClientPortal.Application.DTOs.Projects;

/// <param name="Status">One of <c>ProjectStatus</c> (Planning, Active, OnHold, Completed), as text.</param>
public sealed record ProjectResponse(
    Guid Id,
    ProjectClientResponse Client,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);

/// <param name="DisplayName">The client's name, or their email while the card has no name (same rule as the client list).</param>
public sealed record ProjectClientResponse(Guid Id, string DisplayName);
