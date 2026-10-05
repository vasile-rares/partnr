namespace ClientPortal.Application.Exceptions;

/// <summary>An expected failure that maps to an HTTP status code (see <c>ApiExceptionHandler</c> in the Api).</summary>
public abstract class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
