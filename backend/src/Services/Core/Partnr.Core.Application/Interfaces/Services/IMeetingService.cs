using Partnr.Core.Application.DTOs;
using Partnr.Core.Application.DTOs.Meetings;

namespace Partnr.Core.Application.Interfaces.Services;

public interface IMeetingService
{
    Task<MeetingResponse> CreateAsync(Guid workspaceId, CreateMeetingRequest request, CancellationToken cancellationToken);

    /// <summary>Open to a client too, who gets their own meetings in their own shape; the filters are the firm's and a client's are ignored.</summary>
    Task<RoleResponse<IReadOnlyList<MeetingResponse>, IReadOnlyList<ClientMeetingResponse>>> ListAsync(
        Guid workspaceId,
        bool includeCancelled,
        Guid? clientId,
        Guid? projectId,
        CancellationToken cancellationToken);

    Task<MeetingResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<MeetingResponse> UpdateAsync(Guid workspaceId, Guid id, SaveMeetingRequest request, CancellationToken cancellationToken);

    Task<MeetingResponse> CancelAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>Another go at sending the meeting to Google, once the automatic tries ran out.</summary>
    Task<MeetingResponse> RetrySyncAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
