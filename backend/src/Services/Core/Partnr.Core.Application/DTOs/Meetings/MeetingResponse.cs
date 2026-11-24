using Partnr.Core.Application.DTOs.Projects;

namespace Partnr.Core.Application.DTOs.Meetings;

public sealed record MeetingProjectResponse(Guid Id, string Name);

public sealed record MeetingResponse(
    Guid Id,
    ProjectClientResponse Client,
    MeetingProjectResponse? Project,
    string Title,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    string SyncStatus,
    string? MeetUrl);

/// <summary>What the client sees: no client card, no organiser, nothing about the sync. The project shows only when the client can open it too.</summary>
public sealed record ClientMeetingResponse(
    Guid Id,
    MeetingProjectResponse? Project,
    string Title,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    string? MeetUrl);
