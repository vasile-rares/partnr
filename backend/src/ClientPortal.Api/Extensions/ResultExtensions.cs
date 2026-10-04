using ClientPortal.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToProblemResult(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem response.");
        }

        var status = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = result.Error.Code,
            Detail = result.Error.Description
        })
        { StatusCode = status };
    }
}
