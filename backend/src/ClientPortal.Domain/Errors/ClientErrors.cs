using ClientPortal.SharedKernel.Results;

namespace ClientPortal.Domain.Errors;

public static class ClientErrors
{
    public static readonly Error EmailAlreadyUsed =
        Error.Conflict("Clients.EmailAlreadyUsed", "A client with this email already exists.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Clients.NotFound", $"The client with id '{id}' was not found.");
}
