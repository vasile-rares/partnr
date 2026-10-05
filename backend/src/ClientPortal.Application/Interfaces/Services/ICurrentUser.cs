namespace ClientPortal.Application.Interfaces.Services;

/// <summary>The signed-in user of the current request, as resolved from the auth cookie.</summary>
public interface ICurrentUser
{
    /// <summary>Null when the request is anonymous.</summary>
    Guid? UserId { get; }
}
