namespace ClientPortal.Domain.Enums;

/// <summary>Where a client invitation stands. Expired is derived from the clock; the other states are recorded.</summary>
public enum InvitationStatus
{
    /// <summary>Sent and still usable.</summary>
    Pending,

    /// <summary>Not accepted before its expiry date.</summary>
    Expired,

    /// <summary>Withdrawn by the workspace, replaced by a newer invitation, or dropped when the client was archived.</summary>
    Cancelled,

    /// <summary>The client signed up or signed in with it and is now linked to an account.</summary>
    Accepted
}
