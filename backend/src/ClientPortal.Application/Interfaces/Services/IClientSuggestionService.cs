using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Finds accounts by the start of their email. A directory of accounts, so deliberately narrow (members only, few results).</summary>
public interface IClientSuggestionService
{
    const int MinimumQueryLength = 3;

    const int MaxResults = 5;

    /// <summary>Below <see cref="MinimumQueryLength"/> characters gives an empty list, not an error.</summary>
    Task<IReadOnlyList<ClientSuggestionResponse>> SuggestAsync(Guid workspaceId, string? emailStart, CancellationToken cancellationToken);
}
