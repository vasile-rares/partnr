namespace Partnr.Domain.Enums;

public enum MeetingStatus
{
    Scheduled,

    /// <summary>Kept in the history, never deleted.</summary>
    Cancelled,
}
