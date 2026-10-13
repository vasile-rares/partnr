using ClientPortal.Application.Common;
using ClientPortal.Application.DTOs.Invitations;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class InvitationService(
    IClientInvitationRepository invitations,
    IClientRepository clients,
    IWorkspaceRepository workspaces,
    IIdentityService identity,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IInvitationService
{
    public async Task<InvitationPreviewResponse> GetAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(token, cancellationToken);
        var workspace = await RequireWorkspaceAsync(invitation.WorkspaceId, cancellationToken);
        var accountExists = await identity.FindByEmailAsync(invitation.Email, cancellationToken) is not null;
        var status = invitation.StatusAt(Now());

        return new InvitationPreviewResponse(workspace.Name, invitation.Email, status.ToString(), accountExists, invitation.ExpiresAtUtc);
    }

    public async Task<AcceptedInvitationResponse> AcceptAsync(string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");
        var invitation = await RequirePendingAsync(token, cancellationToken);

        var account = await identity.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("Not signed in.");

        // The invitation proves the address, so it is only good for the account that owns that address.
        if (Client.NormalizeEmail(account.Email) != invitation.Email)
        {
            throw new ForbiddenException(
                "This invitation was sent to a different email address. Sign in with the address it was sent to.");
        }

        return await LinkAsync(invitation, userId, cancellationToken);
    }

    public async Task<AcceptedInvitationResponse> RegisterAsync(
        string token,
        RegisterViaInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var invitation = await RequirePendingAsync(token, cancellationToken);

        if (await identity.FindByEmailAsync(invitation.Email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists. Sign in to accept the invitation.");
        }

        var account = await identity.CreateAsync(invitation.Email, request.FullName.Trim(), request.Password, cancellationToken);
        await identity.UpdateProfileAsync(
            account.Id,
            account.FullName,
            TextHelpers.Optional(request.Phone),
            TextHelpers.Optional(request.Company),
            cancellationToken);

        var accepted = await LinkAsync(invitation, account.Id, cancellationToken);
        await identity.SignInAsync(account.Id, cancellationToken);

        return accepted;
    }

    private async Task<AcceptedInvitationResponse> LinkAsync(ClientInvitation invitation, Guid userId, CancellationToken cancellationToken)
    {
        var workspace = await RequireWorkspaceAsync(invitation.WorkspaceId, cancellationToken);

        // A member already sees the whole workspace; being its client as well would mix the two roles.
        if (workspace.RoleOf(userId) is not null)
        {
            throw new ConflictException($"You are a member of {workspace.Name}, so you cannot also be its client.");
        }

        if (await clients.IsLinkedAsync(workspace.Id, userId, cancellationToken))
        {
            throw new ConflictException($"You already have access to {workspace.Name} as a client.");
        }

        var client = await clients.GetByIdAsync(workspace.Id, invitation.ClientId, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

        var now = Now();
        client.LinkTo(userId, now);
        invitation.Accept(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Using a link sent to the address proves the person owns it.
        await identity.ConfirmEmailAsync(userId, cancellationToken);

        return new AcceptedInvitationResponse(workspace.Id, workspace.Name);
    }

    /// <summary>The invitation behind the link; unknown links are NotFound, with no hint as to why.</summary>
    private async Task<ClientInvitation> FindAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > ClientInvitation.TokenMaxLength)
        {
            throw new NotFoundException("This invitation link is not valid.");
        }

        return await invitations.GetByTokenHashAsync(ClientInvitation.HashToken(token), cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");
    }

    private async Task<ClientInvitation> RequirePendingAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(token, cancellationToken);

        return invitation.StatusAt(Now()) switch
        {
            InvitationStatus.Pending => invitation,
            InvitationStatus.Expired => throw new ConflictException("This invitation has expired. Ask for a new one."),
            InvitationStatus.Cancelled => throw new ConflictException("This invitation was cancelled. Ask for a new one."),
            _ => throw new ConflictException("This invitation was already used.")
        };
    }

    private async Task<Workspace> RequireWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await workspaces.GetByIdAsync(workspaceId, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
