using System.Text.RegularExpressions;
using Partnr.Contracts.Events;
using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Exceptions;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Application.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.UnitTests.Application;

public partial class GoogleConnectionServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeGoogleConnectionRepository _connections = new();
    private readonly FakeMeetingSyncRepository _syncs = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeGoogleOAuthClient _google = new();
    private readonly FakeSecretProtector _protector = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly GoogleConnectionService _service;

    public GoogleConnectionServiceTests()
    {
        _currentUser = new FakeCurrentUser(_userId);
        _service = new GoogleConnectionService(_currentUser, _connections, _syncs, _events, _google, _protector, _unitOfWork, _time);
    }

    [Fact]
    public async Task Disconnect_LeavesWhatIsAlreadyInGoogle_ParksWhatWasStillWaiting_AndTellsTheOtherServices()
    {
        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);
        _events.Published.Clear();
        var waiting = NewRow(_userId);
        var failed = NewRow(_userId);
        failed.RecordSyncFailure(failed.Version, _time.Now.UtcDateTime.AddMinutes(1)); // one more try is left: still Pending
        var inStep = NewRow(_userId);
        inStep.RecordSynced(inStep.Version, "event", "https://meet.google.com/x", _time.Now.UtcDateTime);
        var someoneElses = NewRow(Guid.NewGuid());

        await _service.DisconnectAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, waiting.Status);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, failed.Status);
        Assert.Equal(MeetingSyncStatus.Synced, inStep.Status);
        Assert.Equal(MeetingSyncStatus.Pending, someoneElses.Status);
        Assert.Equal(new GoogleDisconnected(_userId), Assert.Single(_events.Published));
    }

    [Fact]
    public async Task Complete_PutsWhatWaitedForTheReconnectBackInTheQueue_AndTellsTheOtherServices()
    {
        var waiting = NewRow(_userId);
        waiting.MarkNeedsReconnect();
        var inStep = NewRow(_userId);
        inStep.RecordSynced(inStep.Version, "event", null, _time.Now.UtcDateTime);
        var someoneElses = NewRow(Guid.NewGuid());
        someoneElses.MarkNeedsReconnect();

        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Pending, waiting.Status);
        Assert.Equal(_time.Now.UtcDateTime, waiting.NextSyncAtUtc);
        Assert.Equal(MeetingSyncStatus.Synced, inStep.Status);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, someoneElses.Status);
        Assert.Equal(new GoogleConnected(_userId), Assert.Single(_events.Published));
    }

    [Fact]
    public async Task Complete_DoesNotRevive_AMeetingThatAlreadyEnded()
    {
        var over = NewRow(_userId);
        over.MarkNeedsReconnect();
        _time.Now = new DateTimeOffset(over.EndsAtUtc.AddMinutes(1), TimeSpan.Zero);

        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, over.Status);
    }

    [Theory]
    [InlineData(null, "NotConnected")]
    [InlineData("connected", "Connected")]
    [InlineData("needs-reconnect", "NeedsReconnect")]
    public async Task StatusOf_AnyAccount_SaysWhereItsConnectionStands(string? setup, string expected)
    {
        if (setup is not null)
        {
            await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);
            if (setup == "needs-reconnect")
            {
                _connections.Items.Single().MarkNeedsReconnect();
            }
        }

        Assert.Equal(expected, await _service.StatusOfAsync(_userId, CancellationToken.None));
    }

    private MeetingSync NewRow(Guid organizer)
    {
        var start = _time.Now.UtcDateTime.AddDays(1);
        var row = MeetingSync.Create(Guid.NewGuid());
        row.Apply(1, organizer, "Kick-off", null, start, start.AddHours(1), "contact@acme.com", false, _time.Now.UtcDateTime);
        _syncs.Items.Add(row);

        return row;
    }

    [Fact]
    public async Task Get_WithoutAConnection_SaysNotConnected()
    {
        var response = await _service.GetAsync(CancellationToken.None);

        Assert.Equal(new GoogleConnectionResponse(true, "NotConnected", null), response);
    }

    [Fact]
    public async Task Get_WhenTheServerHasNoGoogleCredentials_SaysItIsNotAvailable()
    {
        _google.IsConfigured = false;

        Assert.False((await _service.GetAsync(CancellationToken.None)).Available);
    }

    [Fact]
    public async Task Start_ReturnsTheGoogleAddressWithAStateOfOurs()
    {
        var url = (await _service.StartAsync(null, CancellationToken.None)).Url;

        Assert.StartsWith("https://google.test/consent?state=", url);
    }

    [Fact]
    public async Task Start_WithoutGoogleCredentials_IsAConflict()
    {
        _google.IsConfigured = false;

        await Assert.ThrowsAsync<ConflictException>(() => _service.StartAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task TheReturnPath_TravelsInTheState_AndStillConnects()
    {
        var url = (await _service.StartAsync("/w/abc-123/calendar", CancellationToken.None)).Url;
        var state = StatePattern().Match(url).Groups[1].Value;

        Assert.Equal("/w/abc-123/calendar", _service.ReturnPathOf(state));
        Assert.Equal(GoogleConnectOutcome.Connected, await _service.CompleteAsync("code", state, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example/x")]
    [InlineData("/calendar?next=1")]
    [InlineData("/a|b")]
    [InlineData("calendar")]
    public async Task APathThatIsNotOurOwn_IsLeftOut(string returnTo)
    {
        var state = StatePattern().Match((await _service.StartAsync(returnTo, CancellationToken.None)).Url).Groups[1].Value;

        Assert.Null(_service.ReturnPathOf(state));
        Assert.Equal(GoogleConnectOutcome.Connected, await _service.CompleteAsync("code", state, null, CancellationToken.None));
    }

    [Fact]
    public void ReturnPathOf_AStateThatIsNotOurs_IsNull()
    {
        Assert.Null(_service.ReturnPathOf(null));
        Assert.Null(_service.ReturnPathOf("forged"));
    }

    [Fact]
    public async Task Complete_SavesTheTokenProtected_AndNeverAsItCame()
    {
        var state = await NewState();

        var outcome = await _service.CompleteAsync("code", state, null, CancellationToken.None);

        Assert.Equal(GoogleConnectOutcome.Connected, outcome);
        var saved = Assert.Single(_connections.Items);
        Assert.Equal(_userId, saved.UserId);
        Assert.Equal("ana@gmail.example", saved.GoogleEmail);
        Assert.NotEqual("refresh-1", saved.ProtectedRefreshToken);
        Assert.Equal("refresh-1", _protector.Unprotect(GoogleConnectionService.RefreshTokenPurpose, saved.ProtectedRefreshToken));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Complete_ForAnAccountThatAlreadyHasOne_ReplacesIt()
    {
        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);
        _connections.Items[0].MarkNeedsReconnect();
        _google.Grant = new GoogleGrant("refresh-2", "work@gmail.example", true);

        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        var saved = Assert.Single(_connections.Items);
        Assert.Equal(GoogleConnectionStatus.Connected, saved.Status);
        Assert.Equal("work@gmail.example", saved.GoogleEmail);
        Assert.Equal("refresh-2", _protector.Unprotect(GoogleConnectionService.RefreshTokenPurpose, saved.ProtectedRefreshToken));
    }

    [Fact]
    public async Task Complete_WhenThePersonSaidNo_IsDenied_AndSavesNothing()
    {
        var outcome = await _service.CompleteAsync(null, await NewState(), "access_denied", CancellationToken.None);

        Assert.Equal(GoogleConnectOutcome.Denied, outcome);
        Assert.Empty(_connections.Items);
    }

    [Fact]
    public async Task Complete_WhenTheCalendarWasUntickedOnTheConsentScreen_AsksForThePermission()
    {
        _google.Grant = new GoogleGrant("refresh-1", "ana@gmail.example", false);

        var outcome = await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        Assert.Equal(GoogleConnectOutcome.MissingPermission, outcome);
        Assert.Empty(_connections.Items);
    }

    [Theory]
    [InlineData(null, "email")]
    [InlineData("refresh", null)]
    [InlineData("  ", "email")]
    public async Task Complete_WhenGoogleSendsNoRefreshTokenOrEmail_Fails(string? token, string? email)
    {
        _google.Grant = new GoogleGrant(token, email, true);

        var outcome = await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        Assert.Equal(GoogleConnectOutcome.Failed, outcome);
        Assert.Empty(_connections.Items);
    }

    [Fact]
    public async Task Complete_WhenGoogleRefusesTheCode_Fails()
    {
        _google.Grant = null;

        Assert.Equal(GoogleConnectOutcome.Failed, await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("made-up")]
    public async Task Complete_WithAStateThatIsNotOurs_Fails(string? state)
    {
        Assert.Equal(GoogleConnectOutcome.Failed, await _service.CompleteAsync("code", state, null, CancellationToken.None));
        Assert.Empty(_connections.Items);
    }

    [Fact]
    public async Task Complete_WithAStateMadeForAnotherAccount_Fails()
    {
        var someoneElses = await NewState();
        _currentUser.UserId = Guid.NewGuid();

        var outcome = await _service.CompleteAsync("code", someoneElses, null, CancellationToken.None);

        Assert.Equal(GoogleConnectOutcome.Failed, outcome);
        Assert.Empty(_connections.Items);
    }

    [Fact]
    public async Task Complete_AfterTenMinutes_Fails()
    {
        var state = await NewState();
        _time.Now = _time.Now.AddMinutes(10);

        Assert.Equal(GoogleConnectOutcome.Failed, await _service.CompleteAsync("code", state, null, CancellationToken.None));
    }

    [Fact]
    public async Task Disconnect_RevokesTheTokenAtGoogle_AndForgetsIt()
    {
        await _service.CompleteAsync("code", await NewState(), null, CancellationToken.None);

        await _service.DisconnectAsync(CancellationToken.None);

        Assert.Empty(_connections.Items);
        Assert.Equal(["refresh-1"], _google.Revoked);
    }

    [Fact]
    public async Task Disconnect_WithoutAConnection_DoesNothing()
    {
        await _service.DisconnectAsync(CancellationToken.None);

        Assert.Empty(_google.Revoked);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.CompleteAsync("c", "s", null, CancellationToken.None));
    }

    private async Task<string> NewState() =>
        StatePattern().Match((await _service.StartAsync(null, CancellationToken.None)).Url).Groups[1].Value;

    [GeneratedRegex("state=(.+)$")]
    private static partial Regex StatePattern();
}
