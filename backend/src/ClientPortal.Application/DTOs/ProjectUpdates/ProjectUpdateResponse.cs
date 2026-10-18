namespace ClientPortal.Application.DTOs.ProjectUpdates;

public sealed record ProjectUpdateResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Body,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SharedAtUtc,
    DateTime? ArchivedAtUtc);
