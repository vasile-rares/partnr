using Partnr.Workspaces.Application.DTOs.Approvals;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Mappings;

internal static class ApprovalMappings
{
    public const string NoApproval = "None";

    public static ApprovalResponse ToResponse(this ApprovalRequest request) =>
        new(request.Id, request.Status.ToString(), request.Note, request.RequestedAtUtc, request.Message, request.ResolvedAtUtc);

    /// <summary>The status of the latest round that was not withdrawn: a withdrawn request leaves what stood before it.</summary>
    public static string ShownStatus(this IEnumerable<ApprovalRequest> roundsNewestFirst) =>
        roundsNewestFirst.FirstOrDefault(r => r.Status != ApprovalStatus.Withdrawn)?.Status.ToString() ?? NoApproval;
}
