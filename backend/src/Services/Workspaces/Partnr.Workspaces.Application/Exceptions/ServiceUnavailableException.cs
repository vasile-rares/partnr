namespace Partnr.Workspaces.Application.Exceptions;

/// <summary>Another service this request needs could not be reached: not the caller's fault, and worth trying again in a moment.</summary>
public sealed class ServiceUnavailableException(string message, Exception? inner = null) : AppException(message, 503)
{
    public Exception? Cause { get; } = inner;
}
