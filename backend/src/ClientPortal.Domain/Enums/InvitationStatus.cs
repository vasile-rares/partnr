namespace ClientPortal.Domain.Enums;

/// <summary>Where a client invitation stands. Expired is derived from the clock; the other states are recorded.</summary>
public enum InvitationStatus
{
    Pending,

    Expired,

    Cancelled,

    Accepted
}
