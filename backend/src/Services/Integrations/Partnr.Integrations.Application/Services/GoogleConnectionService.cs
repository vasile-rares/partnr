using System.Globalization;
using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Exceptions;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.Application.Services;

internal sealed class GoogleConnectionService(
    ICurrentUser currentUser,
    IGoogleConnectionRepository connections,
    IGoogleOAuthClient google,
    ISecretProtector protector,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IGoogleConnectionService
{
    public const string RefreshTokenPurpose = "google-refresh-token";

    private const string StatePurpose = "google-oauth-state";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public async Task<GoogleConnectionResponse> GetAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.GetForUserAsync(RequireUserId(), cancellationToken);

        return connection is null
            ? new GoogleConnectionResponse(google.IsConfigured, "NotConnected", null)
            : new GoogleConnectionResponse(google.IsConfigured, connection.Status.ToString(), connection.GoogleEmail);
    }

    public Task<GoogleConnectUrlResponse> StartAsync(CancellationToken cancellationToken)
    {
        if (!google.IsConfigured)
        {
            throw new ConflictException("Google Calendar is not set up on this server.");
        }

        // Bound to this account and short-lived: a link made for someone else cannot connect their Google to this one.
        var expires = (time.GetUtcNow() + StateLifetime).ToUnixTimeSeconds();
        var state = protector.Protect(StatePurpose, $"{RequireUserId():N}|{expires}");

        return Task.FromResult(new GoogleConnectUrlResponse(google.AuthorizationUrl(state)));
    }

    public async Task<GoogleConnectOutcome> CompleteAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        if (!StateIsOurs(state, userId))
        {
            return GoogleConnectOutcome.Failed;
        }

        if (!string.IsNullOrEmpty(error))
        {
            return GoogleConnectOutcome.Denied;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return GoogleConnectOutcome.Failed;
        }

        var grant = await google.ExchangeCodeAsync(code, cancellationToken);
        if (grant is null || string.IsNullOrWhiteSpace(grant.RefreshToken) || string.IsNullOrWhiteSpace(grant.Email))
        {
            return GoogleConnectOutcome.Failed;
        }

        // The permission screen lets people untick what they do not want to give.
        if (!grant.CanUseCalendar)
        {
            return GoogleConnectOutcome.MissingPermission;
        }

        var protectedToken = protector.Protect(RefreshTokenPurpose, grant.RefreshToken);
        var now = time.GetUtcNow().UtcDateTime;
        var existing = await connections.GetForUserAsync(userId, cancellationToken);
        if (existing is null)
        {
            connections.Add(GoogleConnection.Connect(userId, grant.Email, protectedToken, now));
        }
        else
        {
            existing.Reconnect(grant.Email, protectedToken, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return GoogleConnectOutcome.Connected;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var connection = await connections.GetForUserAsync(userId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        var token = protector.Unprotect(RefreshTokenPurpose, connection.ProtectedRefreshToken);
        if (token is not null)
        {
            await google.RevokeAsync(token, cancellationToken);
        }

        connections.Remove(connection);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool StateIsOurs(string? state, Guid userId)
    {
        var plain = string.IsNullOrEmpty(state) ? null : protector.Unprotect(StatePurpose, state);
        var parts = plain?.Split('|');

        return parts is [var id, var expires]
            && id == userId.ToString("N")
            && long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && time.GetUtcNow().ToUnixTimeSeconds() < seconds;
    }

    private Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Sign in to continue.");
}
