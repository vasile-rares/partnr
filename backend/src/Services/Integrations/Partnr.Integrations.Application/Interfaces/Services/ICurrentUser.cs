namespace Partnr.Integrations.Application.Interfaces.Services;

public interface ICurrentUser
{
    /// <summary>Null when the request is anonymous.</summary>
    Guid? UserId { get; }
}
