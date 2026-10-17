namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Builds links into the web app, for emails.</summary>
public interface IAppLinks
{
    string InvitationUrl(string token);
}
