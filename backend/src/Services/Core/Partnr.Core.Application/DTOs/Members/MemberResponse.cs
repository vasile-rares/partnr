namespace Partnr.Core.Application.DTOs.Members;

/// <param name="Name">The account's name, or its email when it has none.</param>
public sealed record MemberResponse(Guid UserId, string Name, string Email, string Role, DateTime JoinedAtUtc);
