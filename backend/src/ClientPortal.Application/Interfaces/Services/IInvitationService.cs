using ClientPortal.Application.DTOs.Invitations;

namespace ClientPortal.Application.Interfaces.Services;

public interface IInvitationService
{
    Task<InvitationPreviewResponse> GetAsync(string token, CancellationToken cancellationToken);

    /// <summary>The signed-in account's email must be the invited one.</summary>
    Task<AcceptedInvitationResponse> AcceptAsync(string token, CancellationToken cancellationToken);

    /// <summary>Creates the account with the invited email, links the client and signs in.</summary>
    Task<AcceptedInvitationResponse> RegisterAsync(string token, RegisterViaInvitationRequest request, CancellationToken cancellationToken);
}
