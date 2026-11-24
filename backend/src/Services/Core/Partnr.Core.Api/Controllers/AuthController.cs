using Partnr.Core.Application.DTOs.Auth;
using Partnr.Core.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Partnr.Core.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService service, IPasswordResetService passwordReset) : ControllerBase
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

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordReset.RequestAsync(request, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("reset-password/{token}")]
    public async Task<IActionResult> CheckPasswordReset(string token, CancellationToken cancellationToken) =>
        Ok(await passwordReset.CheckAsync(token, cancellationToken));

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        Ok(await passwordReset.ResetAsync(request, cancellationToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await service.LogoutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await service.GetCurrentUserAsync(cancellationToken));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateProfileAsync(request, cancellationToken));
}
