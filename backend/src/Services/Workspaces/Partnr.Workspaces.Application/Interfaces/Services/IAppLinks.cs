namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>Builds links into the web app, for emails.</summary>
public interface IAppLinks
{
    string InvitationUrl(string token);

    /// <summary>A page of the web app, from a path that starts with a slash (it may carry a query).</summary>
    string Url(string path);
}
