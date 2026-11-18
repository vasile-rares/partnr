namespace Partnr.Workspaces.Application.Exceptions;

public sealed class UnauthorizedException(string message) : AppException(message, 401);
