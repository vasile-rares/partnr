using FluentValidation;

namespace ClientPortal.Application.Features.Clients.Commands.RegisterClient;

public sealed class RegisterClientValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}
