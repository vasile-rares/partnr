using Partnr.Core.Application.DTOs.Members;

namespace Partnr.Core.Application.Interfaces.Services;

public interface IMemberService
{
    /// <summary>Any member sees the team, the owner first.</summary>
    Task<IReadOnlyList<MemberResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>Owner only. The owner cannot be removed (409), and neither can a member who still organises upcoming meetings (409).</summary>
    Task RemoveAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);
}
