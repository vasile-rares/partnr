using System.ComponentModel.DataAnnotations;
using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.UnitTests.Application;

public class RegisterClientRequestTests
{
    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(IsValid(new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com" }));

    [Fact]
    public void AnEmailAlone_Passes() =>
        Assert.True(IsValid(new RegisterClientRequest { Email = "contact@acme.com" }));

    [Fact]
    public void ATooLongName_Fails() =>
        Assert.False(IsValid(new RegisterClientRequest { Name = new string('a', 201), Email = "contact@acme.com" }));

    [Theory]
    [InlineData("Acme", "not-an-email")]
    [InlineData("Acme", "")]
    public void InvalidRequest_Fails(string name, string email) =>
        Assert.False(IsValid(new RegisterClientRequest { Name = name, Email = email }));

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
}
