using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Approvals;

public sealed record RequestApprovalRequest
{
    [MaxLength(2000)]
    public string? Note { get; init; }
}
