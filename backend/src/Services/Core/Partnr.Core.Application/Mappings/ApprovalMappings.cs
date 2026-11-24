using Partnr.Core.Application.DTOs.Approvals;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Mappings;

internal static class ApprovalMappings
{
    public const string NoApproval = "None";

    public static ApprovalResponse ToResponse(this ApprovalRequest request) =>
        new(request.Id, request.Status.ToString(), request.Note, request.RequestedAtUtc, request.Message, request.ResolvedAtUtc);

    /// <summary>The status of the latest round that was not withdrawn: a withdrawn request leaves what stood before it.</summary>
    public static string ShownStatus(this IEnumerable<ApprovalRequest> roundsNewestFirst) =>
        roundsNewestFirst.FirstOrDefault(r => r.Status != ApprovalStatus.Withdrawn)?.Status.ToString() ?? NoApproval;
}
