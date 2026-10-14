namespace ClientPortal.Domain.Enums;

/// <summary>Where a project stands, set by the workspace. It is never changed automatically.</summary>
public enum ProjectStatus
{
    /// <summary>Being prepared: a draft the client does not see yet.</summary>
    Planning,

    /// <summary>Work is under way.</summary>
    Active,

    /// <summary>Paused, for example while waiting on the client.</summary>
    OnHold,

    /// <summary>Delivered.</summary>
    Completed
}
