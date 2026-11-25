using Partnr.Core.Application.Common;
using Partnr.Core.Application.DTOs.Invitations;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Services;

/// <summary>What the person holding a link does with it. The link is for a client's portal or for the workspace's team, and the token tells which.</summary>
internal sealed class InvitationService(
    IClientInvitationRepository invitations,
    IMemberInvitationRepository memberInvitations,
    IClientRepository clients,
    IWorkspaceRepository workspaces,
    IIdentityService identity,
    ICurrentUser currentUser,
    INotificationPublisher notifications,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IInvitationService
{
    public async Task<InvitationPreviewResponse> GetAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await FindAsync(token, cancellationToken);
        var workspace = await RequireWorkspaceAsync(invitation.WorkspaceId, cancellationToken);
        var accountExists = await identity.FindByEmailAsync(invitation.Email, cancellationToken) is not null;
        var status = invitation.StatusAt(Now());

        return new InvitationPreviewResponse(workspace.Name, invitation.Email, status.ToString(), accountExists, invitation.ExpiresAtUtc, invitation.Kind);
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

        return await UseAsync(invitation, userId, cancellationToken);
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

        var accepted = await UseAsync(invitation, account.Id, cancellationToken);
        await identity.SignInAsync(account.Id, cancellationToken);

        return accepted;
    }

    private Task<AcceptedInvitationResponse> UseAsync(FoundInvitation invitation, Guid userId, CancellationToken cancellationToken) =>
        invitation.Client is { } client
            ? LinkAsync(client, userId, cancellationToken)
            : JoinAsync(invitation.Member!, userId, cancellationToken);

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
        await notifications.InvitationAcceptedAsync(workspace, client, invitation, userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Using a link sent to the address proves the person owns it.
        await identity.ConfirmEmailAsync(userId, cancellationToken);

        return new AcceptedInvitationResponse(workspace.Id, workspace.Name);
    }

    private async Task<AcceptedInvitationResponse> JoinAsync(MemberInvitation invitation, Guid userId, CancellationToken cancellationToken)
    {
        var workspace = await RequireWorkspaceAsync(invitation.WorkspaceId, cancellationToken);

        if (workspace.RoleOf(userId) is not null)
        {
            throw new ConflictException($"You are already a member of {workspace.Name}.");
        }

        // A client sees only their own projects; the team sees everything, so the two cannot be one account in one workspace.
        if (await clients.IsLinkedAsync(workspace.Id, userId, cancellationToken))
        {
            throw new ConflictException($"You have access to {workspace.Name} as a client, so you cannot also join its team.");
        }

        var now = Now();
        workspace.AddMember(userId, WorkspaceRole.Member);
        invitation.Accept(now);
        await notifications.MemberJoinedAsync(workspace, invitation, userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await identity.ConfirmEmailAsync(userId, cancellationToken);

        return new AcceptedInvitationResponse(workspace.Id, workspace.Name);
    }

    private async Task<FoundInvitation> FindAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > ClientInvitation.TokenMaxLength)
        {
            throw new NotFoundException("This invitation link is not valid.");
        }

        // Both kinds hash the token the same way, so one hash finds it in either table.
        var hash = ClientInvitation.HashToken(token);
        var client = await invitations.GetByTokenHashAsync(hash, cancellationToken);
        if (client is not null)
        {
            return new FoundInvitation(client, null);
        }

        var member = await memberInvitations.GetByTokenHashAsync(hash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

        return new FoundInvitation(null, member);
    }

    private async Task<FoundInvitation> RequirePendingAsync(string token, CancellationToken cancellationToken)
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
