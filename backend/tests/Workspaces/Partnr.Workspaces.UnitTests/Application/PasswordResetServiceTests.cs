using System.Text.RegularExpressions;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Services;
using Partnr.Workspaces.Domain.Common;

namespace Partnr.Workspaces.UnitTests.Application;

public partial class PasswordResetServiceTests
{
    private const string Email = "ana@example.com";

    private readonly FakeIdentityService _identity = new();
    private readonly FakePasswordResetTokenRepository _tokens = new();
    private readonly FakeBackgroundEmailQueue _emails = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly PasswordResetService _service;

    public PasswordResetServiceTests()
    {
        _service = new PasswordResetService(_identity, _tokens, _emails, new FakeAppLinks(), _unitOfWork, _time);
    }

    [Fact]
    public async Task Request_ForAnAccount_StoresOnlyTheHash_AndQueuesAnEmailWithTheLink()
    {
        var account = _identity.AddAccount(Email, "old password", "Ana Pop");

        await _service.RequestAsync(new ForgotPasswordRequest { Email = "  ANA@example.com " }, CancellationToken.None);

        var stored = Assert.Single(_tokens.Items);
        var message = Assert.Single(_emails.Queued);
        var token = TokenIn(message.TextBody);
        Assert.Equal(account.Id, stored.UserId);
        Assert.Equal(SecureToken.Hash(token), stored.TokenHash);
        Assert.DoesNotContain(stored.TokenHash, message.TextBody);
        Assert.Equal(Email, message.To);
        Assert.Equal("Reset your Partnr password", message.Subject);
        Assert.StartsWith("Hi Ana Pop,", message.TextBody);
    }

    [Fact]
    public async Task Request_ForAnUnknownEmail_DoesNothing()
    {
        await _service.RequestAsync(new ForgotPasswordRequest { Email = "nobody@example.com" }, CancellationToken.None);

        Assert.Empty(_tokens.Items);
        Assert.Empty(_emails.Queued);
    }

    [Fact]
    public async Task Request_WithinAMinuteOfTheLast_SendsNothing()
    {
        _identity.AddAccount(Email, "old password");
        await Request();

        _time.Now = _time.Now.AddSeconds(30);
        await Request();

        Assert.Single(_tokens.Items);
        Assert.Single(_emails.Queued);
    }

    [Fact]
    public async Task Request_AfterTheCooldown_CancelsTheOlderLink()
    {
        _identity.AddAccount(Email, "old password");
        await Request();
        var first = TokenIn(_emails.Queued[0].TextBody);

        _time.Now = _time.Now.AddMinutes(2);
        await Request();

        Assert.Equal(2, _emails.Queued.Count);
        Assert.NotNull(_tokens.Items[0].CancelledAtUtc);
        Assert.True(_tokens.Items[1].IsOpen);
        var replaced = await Assert.ThrowsAsync<ConflictException>(() => _service.CheckAsync(first, CancellationToken.None));
        Assert.Contains("newer link", replaced.Message);
    }

    [Fact]
    public async Task Check_ShowsWhoseAccountItIs()
    {
        _identity.AddAccount(Email, "old password");
        await Request();

        var preview = await _service.CheckAsync(TokenIn(_emails.Queued[0].TextBody), CancellationToken.None);

        Assert.Equal(Email, preview.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-one-of-ours")]
    public async Task Check_OfALinkThatIsNotOurs_IsNotFound(string token) =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CheckAsync(token, CancellationToken.None));

    [Fact]
    public async Task Check_AfterAnHour_SaysTheLinkExpired()
    {
        _identity.AddAccount(Email, "old password");
        await Request();

        _time.Now = _time.Now.AddHours(1);

        var expired = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CheckAsync(TokenIn(_emails.Queued[0].TextBody), CancellationToken.None));
        Assert.Contains("expired", expired.Message);
    }

    [Fact]
    public async Task Reset_SetsThePassword_UsesTheLink_AndSignsIn()
    {
        var account = _identity.AddAccount(Email, "old password");
        await Request();
        var token = TokenIn(_emails.Queued[0].TextBody);

        var signedIn = await _service.ResetAsync(new ResetPasswordRequest { Token = token, Password = "new password 1" }, CancellationToken.None);

        Assert.Equal("new password 1", _identity.ResetPasswords[account.Id]);
        Assert.NotNull(_tokens.Items.Single().UsedAtUtc);
        Assert.Equal(account.Id, _identity.SignedIn!.Id);
        Assert.Equal(account.Id, signedIn.Id);
        var again = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ResetAsync(new ResetPasswordRequest { Token = token, Password = "another one" }, CancellationToken.None));
        Assert.Contains("already used", again.Message);
    }

    [Fact]
    public async Task Reset_WithAPasswordTheRulesRefuse_KeepsTheLinkForAnotherTry()
    {
        _identity.AddAccount(Email, "old password");
        await Request();
        var token = TokenIn(_emails.Queued[0].TextBody);
        _identity.RejectNewPasswords = true;

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.ResetAsync(new ResetPasswordRequest { Token = token, Password = "short" }, CancellationToken.None));

        Assert.True(_tokens.Items.Single().IsOpen);
        Assert.Null(_identity.SignedIn);
    }

    private Task Request() => _service.RequestAsync(new ForgotPasswordRequest { Email = Email }, CancellationToken.None);

    private static string TokenIn(string text) => ResetLink().Match(text).Groups[1].Value;

    [GeneratedRegex("/reset-password/([A-Za-z0-9_-]+)")]
    private static partial Regex ResetLink();
}
