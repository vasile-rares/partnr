namespace ClientPortal.Domain.Enums;

/// <summary>Where one stage of a project stands. Several stages can be in progress at once.</summary>
public enum StageStatus
{
    /// <summary>Still to come.</summary>
    ToDo,

    /// <summary>Being worked on.</summary>
    InProgress,

    /// <summary>Finished.</summary>
    Done
}
