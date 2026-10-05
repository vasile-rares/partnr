using System.ComponentModel.DataAnnotations;
using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.UnitTests.Application;

public class RegisterRequestTests
{
    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(IsValid(new RegisterRequest { Email = "me@acme.com", Password = "correct horse", FullName = "Ada Lovelace" }));

    [Theory]
    [InlineData("", "correct horse", "Ada")]
    [InlineData("not-an-email", "correct horse", "Ada")]
    [InlineData("me@acme.com", "", "Ada")]
    [InlineData("me@acme.com", "short", "Ada")]
    [InlineData("me@acme.com", "correct horse", "")]
    public void InvalidRequest_Fails(string email, string password, string fullName) =>
        Assert.False(IsValid(new RegisterRequest { Email = email, Password = password, FullName = fullName }));

    [Fact]
    public void PasswordOfExactlyEightCharacters_Passes() =>
        Assert.True(IsValid(new RegisterRequest { Email = "me@acme.com", Password = "12345678", FullName = "Ada" }));

    [Fact]
    public void PasswordLongerThanTheHashingLimit_Fails() =>
        Assert.False(IsValid(new RegisterRequest { Email = "me@acme.com", Password = new string('a', 129), FullName = "Ada" }));

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
}
