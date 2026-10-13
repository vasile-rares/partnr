using System.ComponentModel.DataAnnotations;
using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.UnitTests.Application;

public class UpdateProfileRequestTests
{
    [Fact]
    public void NameOnly_Passes() =>
        Assert.True(IsValid(new UpdateProfileRequest { FullName = "Ana Pop" }));

    [Fact]
    public void AllDetails_Pass() =>
        Assert.True(IsValid(new UpdateProfileRequest { FullName = "Ana Pop", Phone = "+40 721 000 000", Company = "Acme SRL" }));

    [Theory]
    [InlineData("", null, null)]
    [InlineData("   ", null, null)]
    [InlineData("Ana", "123456789012345678901234567890123456789012345678901", null)]
    public void InvalidRequest_Fails(string fullName, string? phone, string? company) =>
        Assert.False(IsValid(new UpdateProfileRequest { FullName = fullName, Phone = phone, Company = company }));

    [Fact]
    public void TooLongCompany_Fails() =>
        Assert.False(IsValid(new UpdateProfileRequest { FullName = "Ana", Company = new string('a', 201) }));

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
}
