using System.ComponentModel.DataAnnotations;
using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.UnitTests.Application;

public class LoginRequestTests
{
    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(IsValid(new LoginRequest { Email = "me@acme.com", Password = "anything" }));

    [Theory]
    [InlineData("", "anything")]
    [InlineData("not-an-email", "anything")]
    [InlineData("me@acme.com", "")]
    public void InvalidRequest_Fails(string email, string password) =>
        Assert.False(IsValid(new LoginRequest { Email = email, Password = password }));

    // Login must not enforce the registration policy (min length): old or odd passwords still get the generic 401, not a 400.
    [Fact]
    public void ShortPassword_StillPassesValidation() =>
        Assert.True(IsValid(new LoginRequest { Email = "me@acme.com", Password = "short" }));

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
}
