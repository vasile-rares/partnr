using ClientPortal.Application.DTOs.Invitations;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Opening and accepting an invitation, from the client's side. The token in the link is the only credential for reading it.</summary>
public interface IInvitationService
{
    /// <exception cref="Common.Exceptions.NotFoundException">The link is not valid.</exception>
    Task<InvitationPreviewResponse> GetAsync(string token, CancellationToken cancellationToken);

    /// <summary>Accepts as the signed-in user, whose email must be the invited one.</summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">Nobody is signed in.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">The link is not valid.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">The signed-in account has a different email.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The invitation expired, was cancelled or used, or the user is a member of the workspace or already its client.</exception>
    Task<AcceptedInvitationResponse> AcceptAsync(string token, CancellationToken cancellationToken);

    /// <summary>Creates the account with the invited email, links the client to it and signs in.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The link is not valid.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The invitation cannot be used, or an account with this email already exists (sign in instead).</exception>
    /// <exception cref="Common.Exceptions.ValidationFailedException">The password does not meet the policy.</exception>
    Task<AcceptedInvitationResponse> RegisterAsync(string token, RegisterViaInvitationRequest request, CancellationToken cancellationToken);
}
