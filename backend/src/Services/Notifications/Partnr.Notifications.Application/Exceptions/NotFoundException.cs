namespace Partnr.Notifications.Application.Exceptions;

public sealed class NotFoundException(string message) : AppException(message, 404);
