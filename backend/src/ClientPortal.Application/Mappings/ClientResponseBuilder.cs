using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

/// <summary>
/// Turns clients into what the workspace sees. This is where the ownership rule lives: a client who has an account shows
/// the account's name, email, phone and company (no copy that could drift), and the portal status comes from the invitation.
/// </summary>
internal sealed class ClientResponseBuilder(IIdentityService identity, IClientInvitationRepository invitations, TimeProvider time)
{
    public async Task<ClientResponse> BuildAsync(Client client, CancellationToken cancellationToken) =>
        (await BuildAsync([client], cancellationToken))[0];

    public async Task<IReadOnlyList<ClientResponse>> BuildAsync(IReadOnlyList<Client> clients, CancellationToken cancellationToken)
    {
        if (clients.Count == 0)
        {
            return [];
        }

        var now = time.GetUtcNow().UtcDateTime;

        var userIds = clients.Where(c => c.UserId is not null).Select(c => c.UserId!.Value).Distinct().ToList();
        var accounts = userIds.Count == 0
            ? new Dictionary<Guid, CurrentUserResponse>()
            : (await identity.FindByIdsAsync(userIds, cancellationToken)).ToDictionary(a => a.Id);

        var open = (await invitations.ListOpenForClientsAsync(clients.Select(c => c.Id).ToList(), cancellationToken))
            .ToDictionary(i => i.ClientId); // at most one open invitation per client (unique index)

        return clients.Select(client =>
        {
            var account = client.UserId is { } userId && accounts.TryGetValue(userId, out var found) ? found : null;
            open.TryGetValue(client.Id, out var invitation);

            var portal = client.IsLinked
                ? new ClientPortalResponse(nameof(ClientPortalStatus.Active), null)
                : invitation is null
                    ? new ClientPortalResponse(nameof(ClientPortalStatus.NotInvited), null)
                    : new ClientPortalResponse(
                        (invitation.IsPending(now) ? ClientPortalStatus.Invited : ClientPortalStatus.Expired).ToString(),
                        invitation.ExpiresAtUtc);

            // An account that is gone (it should not happen) falls back to the card, so the client does not vanish from the list.
            return new ClientResponse(
                client.Id,
                account?.FullName ?? client.Name,
                account?.Email ?? client.Email,
                account is null ? client.Company : account.Company,
                account is null ? client.Phone : account.Phone,
                client.Notes,
                client.CreatedAtUtc,
                client.ArchivedAtUtc,
                portal,
                DetailsLocked: client.IsLinked || (invitation?.IsPending(now) ?? false));
        }).ToList();
    }
}
