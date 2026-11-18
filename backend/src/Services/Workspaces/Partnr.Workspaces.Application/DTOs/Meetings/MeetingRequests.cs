using System.ComponentModel.DataAnnotations;

namespace Partnr.Workspaces.Application.DTOs.Meetings;

/// <summary>Used to update: it replaces every field and an empty note is cleared. The client never changes.</summary>
public record SaveMeetingRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public Guid? ProjectId { get; init; }

    /// <summary>With an offset (the browser sends "Z"); stored in UTC.</summary>
    [Required]
    public DateTimeOffset? StartsAt { get; init; }

    [Required]
    public DateTimeOffset? EndsAt { get; init; }
}

public sealed record CreateMeetingRequest : SaveMeetingRequest
{
    [Required]
    public Guid ClientId { get; init; }
}
