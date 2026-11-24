using System.ComponentModel.DataAnnotations;

namespace Partnr.Core.Application.DTOs.Approvals;

public sealed record RequestApprovalRequest
{
    [MaxLength(2000)]
    public string? Note { get; init; }
}
