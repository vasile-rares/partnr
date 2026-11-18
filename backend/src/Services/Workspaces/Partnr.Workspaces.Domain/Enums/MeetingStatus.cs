namespace Partnr.Workspaces.Domain.Enums;

public enum MeetingStatus
{
    Scheduled,

    /// <summary>Kept in the history, never deleted.</summary>
    Cancelled,
}
