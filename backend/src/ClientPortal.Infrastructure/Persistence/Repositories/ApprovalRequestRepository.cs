using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ApprovalRequestRepository(ApplicationDbContext dbContext) : IApprovalRequestRepository
{
    public Task<ApprovalRequest?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid deliverableId, Guid id, CancellationToken cancellationToken) =>
        dbContext.ApprovalRequests.FirstOrDefaultAsync(
            a => a.WorkspaceId == workspaceId && a.ProjectId == projectId && a.DeliverableId == deliverableId && a.Id == id,
            cancellationToken);

    public Task<ApprovalRequest?> GetPendingAsync(Guid deliverableId, CancellationToken cancellationToken) =>
        dbContext.ApprovalRequests.FirstOrDefaultAsync(
            a => a.DeliverableId == deliverableId && a.Status == ApprovalStatus.Pending,
            cancellationToken);

    public Task<bool> AnyAsync(Guid deliverableId, CancellationToken cancellationToken) =>
        dbContext.ApprovalRequests.AnyAsync(a => a.DeliverableId == deliverableId, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListForDeliverableAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        CancellationToken cancellationToken) =>
        await dbContext.ApprovalRequests
            .AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId && a.ProjectId == projectId && a.DeliverableId == deliverableId)
            .OrderByDescending(a => a.RequestedAtUtc)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListForProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ApprovalRequests
            .AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId && a.ProjectId == projectId)
            .OrderByDescending(a => a.RequestedAtUtc)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountPendingForClientAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        var ids = projectIds.ToList();
        var counts = await (
            from approval in dbContext.ApprovalRequests
            join deliverable in dbContext.Deliverables on approval.DeliverableId equals deliverable.Id
            where ids.Contains(approval.ProjectId)
                && approval.Status == ApprovalStatus.Pending
                && deliverable.Status == DeliverableStatus.Shared
                && deliverable.ArchivedAtUtc == null
            group approval by approval.ProjectId into perProject
            select new { ProjectId = perProject.Key, Count = perProject.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }

    public void Add(ApprovalRequest request) => dbContext.ApprovalRequests.Add(request);
}
