namespace Partnr.Workspaces.Application.DTOs;

/// <summary>
/// What one route answers when each side gets its own shape: the firm's for a member, the client's for a client.
/// The service decides which one in a single place, so a field of the firm's can never reach a client by being added to the wrong type.
/// </summary>
public sealed class RoleResponse<TMember, TClient>
    where TMember : class
    where TClient : class
{
    private RoleResponse(TMember? member, TClient? client)
    {
        Member = member;
        Client = client;
    }

    public TMember? Member { get; }

    public TClient? Client { get; }

    /// <summary>The one answer to send.</summary>
    public object Body => (object?)Member ?? Client!;

    public static RoleResponse<TMember, TClient> ForMember(TMember response) => new(response, null);

    public static RoleResponse<TMember, TClient> ForClient(TClient response) => new(null, response);
}
