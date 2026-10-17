using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.Application.Interfaces.Services;

public interface IAuthService
{
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<CurrentUserResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task LogoutAsync();

    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<CurrentUserResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);
}
