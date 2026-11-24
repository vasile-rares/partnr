using Partnr.Core.Application.DTOs.ClientView;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Mappings;

internal static class ClientDeliverableMappings
{
    /// <param name="ownRoundsNewestFirst">Rounds of this deliverable only.</param>
    public static ClientDeliverableResponse ToClientResponse(
        this Deliverable deliverable,
        string? stageName,
        IEnumerable<ApprovalRequest> ownRoundsNewestFirst) =>
        new(deliverable.Id, deliverable.Title, deliverable.Description, deliverable.Url, stageName, ownRoundsNewestFirst.ToClientApproval());

    /// <summary>A withdrawn round was the freelancer's change of mind, not the client's business, so it never shows.</summary>
    public static ClientApprovalResponse ToClientApproval(this IEnumerable<ApprovalRequest> roundsNewestFirst)
    {
        var visible = roundsNewestFirst.Where(r => r.Status != ApprovalStatus.Withdrawn).ToList();
        var pending = visible.FirstOrDefault(r => r.IsPending);

        return new ClientApprovalResponse(
            visible.ShownStatus(),
            pending is null ? null : new ClientPendingApprovalResponse(pending.Id, pending.Note, pending.RequestedAtUtc),
            visible
                .Where(r => !r.IsPending)
                .Select(r => new ClientApprovalDecisionResponse(r.Status.ToString(), r.Note, r.Message, r.RequestedAtUtc, r.ResolvedAtUtc!.Value))
                .ToList());
    }
}
