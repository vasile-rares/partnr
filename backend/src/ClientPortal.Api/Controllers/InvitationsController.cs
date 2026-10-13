using ClientPortal.Application.DTOs.Invitations;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

/// <summary>The client's side of an invitation. The token in the route is the credential for reading it.</summary>
[ApiController]
[Route("api/invitations/{token}")]
public sealed class InvitationsController(IInvitationService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get(string token, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(token, cancellationToken));

    // Needs a signed-in user (the fallback policy): the invited address is checked against that account's.
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken) =>
        Ok(await service.AcceptAsync(token, cancellationToken));

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(string token, RegisterViaInvitationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RegisterAsync(token, request, cancellationToken));
}
