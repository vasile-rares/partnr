using Partnr.Contracts.Events;
using Partnr.Identity.Application.DTOs.Auth;
using Partnr.Identity.Application.Exceptions;
using Partnr.Identity.Application.Services;
using Partnr.Identity.Domain.Common;

namespace Partnr.Identity.UnitTests.Application;

public class PasswordResetServiceTests
{
    private const string Email = "ana@example.com";

    private readonly FakeIdentityService _identity = new();
    private readonly FakePasswordResetTokenRepository _tokens = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly PasswordResetService _service;

    private IReadOnlyList<PasswordResetRequested> Sent => _events.Published.OfType<PasswordResetRequested>().ToList();

    public PasswordResetServiceTests()
    {
        _service = new PasswordResetService(_identity, _tokens, _events, _unitOfWork, _time);
    }

    [Fact]
    public async Task Request_ForAnAccount_StoresOnlyTheHash_AndSendsTheToken()
    {
        var account = _identity.AddAccount(Email, "old password", "Ana Pop");

        await _service.RequestAsync(new ForgotPasswordRequest { Email = "  ANA@example.com " }, CancellationToken.None);

        var stored = Assert.Single(_tokens.Items);
        var sent = Assert.Single(Sent);
        Assert.Equal(account.Id, stored.UserId);
        Assert.Equal(SecureToken.Hash(sent.Token), stored.TokenHash);
        Assert.DoesNotContain(stored.TokenHash, sent.Token);
        Assert.Equal(account.Email, sent.RecipientEmail);
        Assert.Equal(account.FullName, sent.RecipientName);
    }

    [Fact]
    public async Task Request_ForAnUnknownEmail_DoesNothing()
    {
        await _service.RequestAsync(new ForgotPasswordRequest { Email = "nobody@example.com" }, CancellationToken.None);

        Assert.Empty(_tokens.Items);
        Assert.Empty(Sent);
    }

    [Fact]
    public async Task Request_WithinAMinuteOfTheLast_SendsNothing()
    {
        _identity.AddAccount(Email, "old password");
        await Request();

        _time.Now = _time.Now.AddSeconds(30);
        await Request();

        Assert.Single(_tokens.Items);
        Assert.Single(Sent);
    }

    [Fact]
    public async Task Request_AfterTheCooldown_CancelsTheOlderLink()
    {
        _identity.AddAccount(Email, "old password");
        await Request();
        var first = Sent[0].Token;

        _time.Now = _time.Now.AddMinutes(2);
        await Request();

        Assert.Equal(2, Sent.Count);
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

        var preview = await _service.CheckAsync(Sent[0].Token, CancellationToken.None);

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
            _service.CheckAsync(Sent[0].Token, CancellationToken.None));
        Assert.Contains("expired", expired.Message);
    }

    [Fact]
    public async Task Reset_SetsThePassword_UsesTheLink_AndSignsIn()
    {
        var account = _identity.AddAccount(Email, "old password");
        await Request();
        var token = Sent[0].Token;

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
        var token = Sent[0].Token;
        _identity.RejectNewPasswords = true;

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.ResetAsync(new ResetPasswordRequest { Token = token, Password = "short" }, CancellationToken.None));

        Assert.True(_tokens.Items.Single().IsOpen);
        Assert.Null(_identity.SignedIn);
    }

    private Task Request() => _service.RequestAsync(new ForgotPasswordRequest { Email = Email }, CancellationToken.None);
}
