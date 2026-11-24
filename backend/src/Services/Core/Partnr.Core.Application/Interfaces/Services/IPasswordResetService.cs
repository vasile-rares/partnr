using Partnr.Core.Application.DTOs.Auth;

namespace Partnr.Core.Application.Interfaces.Services;

public interface IPasswordResetService
{
    /// <summary>Answers the same way whether or not the email has an account: nothing tells which emails exist.</summary>
    Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>404 for a link that is not ours, 409 for one that was used, replaced or has expired.</summary>
    Task<PasswordResetPreviewResponse> CheckAsync(string token, CancellationToken cancellationToken);

    /// <summary>Sets the password, signs out every other session and signs this one in.</summary>
    Task<CurrentUserResponse> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
