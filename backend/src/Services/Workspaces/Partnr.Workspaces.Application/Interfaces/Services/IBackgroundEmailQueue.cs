using Partnr.Workspaces.Application.DTOs.Email;

namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>
/// Sends an email after the request has answered, so how long a request takes never shows whether an email went out.
/// Kept in memory: an email still queued when the API stops is lost, and the person asks again.
/// </summary>
public interface IBackgroundEmailQueue
{
    void Enqueue(EmailMessage message);
}
