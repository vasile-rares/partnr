namespace ClientPortal.SharedKernel.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict
}

public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
}
