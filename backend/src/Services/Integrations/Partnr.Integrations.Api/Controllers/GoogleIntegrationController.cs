using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Integrations.Api.Controllers;

/// <summary>The signed-in account's own Google connection, so no workspace in the route (like /api/notifications).</summary>
[ApiController]
[Route("api/integrations/google")]
public sealed class GoogleIntegrationController(IGoogleConnectionService service, IAppLinks links) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(cancellationToken));

    [HttpPost("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken) =>
        Ok(await service.StartAsync(cancellationToken));

    /// <summary>Google sends the browser here, through the web app's /api rewrite so the session cookie comes along; it ends on a page of the web app.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        var outcome = await service.CompleteAsync(code, state, error, cancellationToken);
        var result = outcome switch
        {
            GoogleConnectOutcome.Connected => "connected",
            GoogleConnectOutcome.Denied => "denied",
            GoogleConnectOutcome.MissingPermission => "permission",
            _ => "error",
        };

        return Redirect(links.Url($"/account?google={result}"));
    }

    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        await service.DisconnectAsync(cancellationToken);

        return NoContent();
    }
}
