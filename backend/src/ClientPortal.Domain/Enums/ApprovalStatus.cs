namespace ClientPortal.Domain.Enums;

/// <summary>Where one approval round stands. A closed round (anything but Pending) never changes again.</summary>
public enum ApprovalStatus
{
    Pending,

    Approved,

    ChangesRequested,

    /// <summary>The freelancer took the request back before the client decided.</summary>
    Withdrawn
}
