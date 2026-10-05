using ClientPortal.Application.Auth;
using ClientPortal.Application.Auth.Dtos.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await service.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), user);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await service.LoginAsync(request, cancellationToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await service.LogoutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await service.GetCurrentUserAsync(cancellationToken));
}
