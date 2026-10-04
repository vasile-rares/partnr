using ClientPortal.Application.Common.Extensions;
using ClientPortal.Application.Common.Interfaces;
using ClientPortal.Application.Common.Messaging;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Errors;
using ClientPortal.Domain.Interfaces;
using ClientPortal.SharedKernel.Results;
using FluentValidation;

namespace ClientPortal.Application.Features.Clients.Commands.RegisterClient;

public sealed class RegisterClientHandler(
    IValidator<RegisterClientCommand> validator,
    IClientRepository clients,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterClientCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterClientCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var email = command.Email.Trim().ToLowerInvariant();
        if (await clients.EmailExistsAsync(email, cancellationToken))
        {
            return ClientErrors.EmailAlreadyUsed;
        }

        var client = Client.Register(command.Name, email);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.Id;
    }
}
