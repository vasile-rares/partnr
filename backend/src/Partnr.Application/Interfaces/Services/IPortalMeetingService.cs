using Partnr.Application.DTOs.Meetings;

namespace Partnr.Application.Interfaces.Services;

public interface IPortalMeetingService
{
    Task<IReadOnlyList<PortalMeetingResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);
}
