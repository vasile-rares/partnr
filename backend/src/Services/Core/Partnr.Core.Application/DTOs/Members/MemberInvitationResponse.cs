namespace Partnr.Core.Application.DTOs.Members;

/// <param name="Status"><c>Pending</c> or <c>Expired</c>: only the open ones are listed.</param>
public sealed record MemberInvitationResponse(Guid Id, string Email, string Status, DateTime CreatedAtUtc, DateTime ExpiresAtUtc);
