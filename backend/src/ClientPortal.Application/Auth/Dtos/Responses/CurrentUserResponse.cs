namespace ClientPortal.Application.Auth.Dtos.Responses;

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName);
