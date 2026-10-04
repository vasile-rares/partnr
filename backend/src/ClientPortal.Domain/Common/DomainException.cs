namespace ClientPortal.Domain.Common;

/// <summary>A broken business rule. The API maps it to 400 Bad Request.</summary>
public sealed class DomainException(string message) : Exception(message);
