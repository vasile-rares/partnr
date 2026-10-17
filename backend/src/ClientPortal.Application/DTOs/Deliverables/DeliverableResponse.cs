namespace ClientPortal.Application.DTOs.Deliverables;

public sealed record DeliverableResponse(
    Guid Id,
    Guid ProjectId,
    Guid? StageId,
    string? StageName,
    string Title,
    string? Description,
    string Url,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);
