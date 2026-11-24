namespace Partnr.Core.Application.Exceptions;

public sealed class ConflictException(string message) : AppException(message, 409);
