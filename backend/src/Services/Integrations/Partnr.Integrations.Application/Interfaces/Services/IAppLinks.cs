namespace Partnr.Integrations.Application.Interfaces.Services;

/// <summary>Builds links into the web app.</summary>
public interface IAppLinks
{
    /// <summary>A page of the web app, from a path that starts with a slash (it may carry a query).</summary>
    string Url(string path);
}
