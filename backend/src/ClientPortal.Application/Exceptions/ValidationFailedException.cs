namespace ClientPortal.Application.Exceptions;

/// <summary>Input that passed the DTO annotations but was rejected by a business rule (e.g. a password policy).</summary>
public sealed class ValidationFailedException(string message) : AppException(message, 400);
