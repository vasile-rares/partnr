using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Services;

internal sealed class ClientSuggestionService(WorkspaceAccess access, IIdentityService identity, IClientRepository clients)
    : IClientSuggestionService
{
    public async Task<IReadOnlyList<ClientSuggestionResponse>> SuggestAsync(
        Guid workspaceId,
        string? emailStart,
        CancellationToken cancellationToken)
    {
        // The access check comes first, so a stranger learns nothing, not even that the query was too short.
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);

        var prefix = emailStart?.Trim() ?? string.Empty;
        if (prefix.Length < IClientSuggestionService.MinimumQueryLength)
        {
            return [];
        }

        // Members cannot accept an invitation to their own workspace. Excluded in the query so the limit still yields useful rows.
        var memberIds = membership.Workspace.Members.Select(m => m.UserId).ToList();
        var accounts = await identity.SearchByEmailStartAsync(
            Client.NormalizeEmail(prefix), IClientSuggestionService.MaxResults, memberIds, cancellationToken);
        if (accounts.Count == 0)
        {
            return [];
        }

        var existing = await clients.ExistingEmailsAsync(workspaceId, accounts.Select(a => Client.NormalizeEmail(a.Email)).ToList(), cancellationToken);

        return accounts
            .Select(a => new ClientSuggestionResponse(a.Email, a.FullName, existing.Contains(Client.NormalizeEmail(a.Email))))
            .ToList();
    }
}
