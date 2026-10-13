namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Builds links into the web app, for emails. The address of the app is configuration, so it lives in Infrastructure.</summary>
public interface IAppLinks
{
    /// <summary>The page where a client opens their invitation.</summary>
    string InvitationUrl(string token);
}
