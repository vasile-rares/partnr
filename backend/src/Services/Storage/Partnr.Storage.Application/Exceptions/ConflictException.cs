namespace Partnr.Storage.Application.Exceptions;

public sealed class ConflictException(string message) : AppException(message, 409);
