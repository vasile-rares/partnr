using ClientPortal.Application.Features.Clients.Commands.RegisterClient;

namespace ClientPortal.Application.Tests.Features.Clients;

public class RegisterClientValidatorTests
{
    private readonly RegisterClientValidator _validator = new();

    [Fact]
    public void ValidCommand_Passes() =>
        Assert.True(_validator.Validate(new RegisterClientCommand("Acme", "contact@acme.com")).IsValid);

    [Theory]
    [InlineData("", "contact@acme.com")]
    [InlineData("Acme", "not-an-email")]
    [InlineData("Acme", "")]
    public void InvalidCommand_Fails(string name, string email) =>
        Assert.False(_validator.Validate(new RegisterClientCommand(name, email)).IsValid);
}
