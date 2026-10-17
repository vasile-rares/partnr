namespace ClientPortal.Application.Exceptions;

/// <summary>The caller may know the resource exists, but may not do this (e.g. a member doing an owner-only action).</summary>
public sealed class ForbiddenException(string message) : AppException(message, 403);
