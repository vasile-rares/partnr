using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>
/// Finds existing accounts by the start of their email, for the "add client" form. This is a directory of accounts, so it is
/// kept narrow: members of a workspace only, at least <see cref="MinimumQueryLength"/> characters, a handful of results,
/// matched on the email only, and rate limited by the API.
/// </summary>
public interface IClientSuggestionService
{
    const int MinimumQueryLength = 3;

    const int MaxResults = 5;

    /// <summary>Fewer than <see cref="MinimumQueryLength"/> characters (after trimming) gives an empty list, not an error.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    Task<IReadOnlyList<ClientSuggestionResponse>> SuggestAsync(Guid workspaceId, string? emailStart, CancellationToken cancellationToken);
}
