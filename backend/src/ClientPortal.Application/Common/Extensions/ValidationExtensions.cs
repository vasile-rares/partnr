using ClientPortal.SharedKernel.Results;
using FluentValidation.Results;

namespace ClientPortal.Application.Common.Extensions;

public static class ValidationExtensions
{
    public static Error ToError(this ValidationResult validationResult) =>
        Error.Validation(
            "Validation.Failed",
            string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));
}
