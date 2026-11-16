using Partnr.Infrastructure.Google;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Partnr.IntegrationTests.Google;

// No container needed: the protector is plain cryptography.
public class SecretProtectorTests
{
    private static SecretProtector Protector(string? key = null) =>
        new(Options.Create(new GoogleOptions { EncryptionKey = key ?? GoogleOptions.NewKey() }));

    [Fact]
    public void Protect_ThenUnprotect_ReturnsTheSecret_AndTheStoredFormHidesIt()
    {
        var protector = Protector();

        var sealedValue = protector.Protect("purpose", "1//refresh-token-ünïcode");

        Assert.DoesNotContain("refresh", sealedValue);
        Assert.Equal("1//refresh-token-ünïcode", protector.Unprotect("purpose", sealedValue));
    }

    [Fact]
    public void Protect_IsUrlSafe_AndDiffersEachTime()
    {
        var protector = Protector();

        var first = protector.Protect("purpose", "same");
        var second = protector.Protect("purpose", "same");

        Assert.NotEqual(first, second);
        Assert.Matches("^[A-Za-z0-9_-]+$", first);
    }

    [Fact]
    public void Unprotect_UnderAnotherPurpose_IsNull()
    {
        var protector = Protector();

        Assert.Null(protector.Unprotect("other", protector.Protect("purpose", "secret")));
    }

    [Fact]
    public void Unprotect_WithAnotherKey_IsNull()
    {
        var sealedValue = Protector().Protect("purpose", "secret");

        Assert.Null(Protector().Unprotect("purpose", sealedValue));
    }

    [Fact]
    public void Unprotect_OfAChangedValue_IsNull()
    {
        var protector = Protector();
        var sealedValue = protector.Protect("purpose", "secret");
        var changed = sealedValue[..^1] + (sealedValue[^1] == 'A' ? 'B' : 'A');

        Assert.Null(protector.Unprotect("purpose", changed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not base64 at all!!")]
    public void Unprotect_OfSomethingElse_IsNull(string value) => Assert.Null(Protector().Unprotect("purpose", value));

    [Theory]
    [InlineData("")]
    [InlineData("c2hvcnQ=")]
    public void WithoutAProperKey_ItRefusesToWork(string key) =>
        Assert.Throws<InvalidOperationException>(() => Protector(key).Protect("purpose", "secret"));

    [Fact]
    public void TheOptionsCheck_RejectsABadKey_OnlyWhenGoogleIsSetUp()
    {
        var validator = new GoogleOptionsValidator(new TestHostEnvironment(Environments.Production));

        Assert.True(validator.Validate(null, new GoogleOptions()).Succeeded);
        Assert.False(validator.Validate(null, Configured(key: "bad")).Succeeded);
        Assert.False(validator.Validate(null, Configured(redirect: "not a url")).Succeeded);
        Assert.True(validator.Validate(null, Configured()).Succeeded);
    }

    [Fact]
    public void TheFakeGoogle_IsRefusedOutsideDevelopment()
    {
        var options = new GoogleOptions
        {
            Fake = true,
            RedirectUri = "http://localhost:3100/api/integrations/google/callback",
            EncryptionKey = GoogleOptions.NewKey(),
        };

        Assert.True(new GoogleOptionsValidator(new TestHostEnvironment(Environments.Development)).Validate(null, options).Succeeded);
        Assert.False(new GoogleOptionsValidator(new TestHostEnvironment(Environments.Production)).Validate(null, options).Succeeded);
        Assert.False(new GoogleOptionsValidator(new TestHostEnvironment("Staging")).Validate(null, options).Succeeded);
    }


    private static GoogleOptions Configured(string? key = null, string redirect = "http://localhost:3000/api/integrations/google/callback") =>
        new() { ClientId = "id", ClientSecret = "secret", RedirectUri = redirect, EncryptionKey = key ?? GoogleOptions.NewKey() };
}
