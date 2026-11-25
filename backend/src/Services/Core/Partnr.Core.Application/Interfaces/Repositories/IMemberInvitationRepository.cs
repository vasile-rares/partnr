using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Interfaces.Repositories;

public interface IMemberInvitationRepository
{
    Task<MemberInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Scoped to a workspace: an invitation of another one is simply not found.</summary>
    Task<MemberInvitation?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>Neither accepted nor cancelled, expired or not. There is at most one per address.</summary>
    Task<MemberInvitation?> GetOpenForEmailAsync(Guid workspaceId, string email, CancellationToken cancellationToken);

    /// <summary>Neither accepted nor cancelled, newest first.</summary>
    Task<IReadOnlyList<MemberInvitation>> ListOpenAsync(Guid workspaceId, CancellationToken cancellationToken);

    void Add(MemberInvitation invitation);
}
