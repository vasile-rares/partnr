using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Mappings;

internal static class PortalDeliverableMappings
{
    /// <param name="ownRoundsNewestFirst">Rounds of this deliverable only.</param>
    public static PortalDeliverableResponse ToPortalResponse(
        this Deliverable deliverable,
        string? stageName,
        IEnumerable<ApprovalRequest> ownRoundsNewestFirst) =>
        new(deliverable.Id, deliverable.Title, deliverable.Description, deliverable.Url, stageName, ownRoundsNewestFirst.ToPortalApproval());

    /// <summary>A withdrawn round was the freelancer's change of mind, not the client's business, so it never shows.</summary>
    public static PortalApprovalResponse ToPortalApproval(this IEnumerable<ApprovalRequest> roundsNewestFirst)
    {
        var visible = roundsNewestFirst.Where(r => r.Status != ApprovalStatus.Withdrawn).ToList();
        var pending = visible.FirstOrDefault(r => r.IsPending);

        return new PortalApprovalResponse(
            visible.ShownStatus(),
            pending is null ? null : new PortalPendingApprovalResponse(pending.Id, pending.Note, pending.RequestedAtUtc),
            visible
                .Where(r => !r.IsPending)
                .Select(r => new PortalApprovalDecisionResponse(r.Status.ToString(), r.Note, r.Message, r.RequestedAtUtc, r.ResolvedAtUtc!.Value))
                .ToList());
    }
}
