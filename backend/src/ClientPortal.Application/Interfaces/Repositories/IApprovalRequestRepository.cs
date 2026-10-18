using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Rounds are never removed: they are the history of approvals.</summary>
public interface IApprovalRequestRepository
{
    Task<ApprovalRequest?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid deliverableId, Guid id, CancellationToken cancellationToken);

    /// <summary>The open round of a deliverable, if any. The caller must already have found the deliverable in its workspace and project.</summary>
    Task<ApprovalRequest?> GetPendingAsync(Guid deliverableId, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Guid deliverableId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ApprovalRequest>> ListForDeliverableAsync(Guid workspaceId, Guid projectId, Guid deliverableId, CancellationToken cancellationToken);

    /// <summary>Every round of every deliverable of the project, newest first.</summary>
    Task<IReadOnlyList<ApprovalRequest>> ListForProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// How many rounds wait for the client, per project, counting only deliverables the client can see. Projects with
    /// nothing waiting are absent. The caller passes projects already found as shared with the client.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountPendingForClientAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    void Add(ApprovalRequest request);
}
