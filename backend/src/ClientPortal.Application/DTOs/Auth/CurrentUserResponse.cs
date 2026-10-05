namespace ClientPortal.Application.DTOs.Auth;

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName);
