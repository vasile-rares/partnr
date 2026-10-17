using ClientPortal.Application.DTOs.Deliverables;

namespace ClientPortal.Application.Interfaces.Services;

public interface IDeliverableService
{
    Task<DeliverableResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveDeliverableRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliverableResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<DeliverableResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<DeliverableResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveDeliverableRequest request,
        CancellationToken cancellationToken);

    Task<DeliverableResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<DeliverableResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<DeliverableResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<DeliverableResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);
}
