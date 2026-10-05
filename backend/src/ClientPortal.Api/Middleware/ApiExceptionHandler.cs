using ClientPortal.Application.Exceptions;
using ClientPortal.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ClientPortal.Api.Middleware;

/// <summary>Maps expected exceptions to ProblemDetails; anything else falls through to the default 500 handler.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            AppException appException => appException.StatusCode,
            DomainException => StatusCodes.Status400BadRequest,
            _ => (int?)null
        };

        if (statusCode is not { } status)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = exception.Message
            }
        });
    }
}
