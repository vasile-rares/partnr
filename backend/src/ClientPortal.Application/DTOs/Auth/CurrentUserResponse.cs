namespace ClientPortal.Application.DTOs.Auth;

/// <summary>The signed-in account. Phone and company are optional profile details, null when not set.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, string? Phone = null, string? Company = null);
