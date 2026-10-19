namespace ClientPortal.Application.DTOs.ProjectFiles;

public sealed record ProjectFileResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);
