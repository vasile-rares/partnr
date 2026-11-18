using Partnr.Workspaces.Application.Common;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.DTOs.Email;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Domain.Common;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Services;

internal sealed class PasswordResetService(
    IIdentityService identity,
    IPasswordResetTokenRepository tokens,
    IBackgroundEmailQueue emails,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IPasswordResetService
{
    private const string InvalidLinkMessage = "This link is not valid.";

    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var account = await identity.FindByEmailAsync(request.Email.Trim(), cancellationToken);
        if (account is null)
        {
            return;
        }

        var now = Now();
        var open = await tokens.GetOpenForUserAsync(account.Id, cancellationToken);
        if (open is not null)
        {
            if (open.IsCoolingDownAt(now))
            {
                return;
            }

            // Cancelled in its own save: the unique index allows one open link per account.
            open.Cancel(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var (token, hash) = SecureToken.New();
        tokens.Add(PasswordResetToken.Create(account.Id, hash, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        emails.Enqueue(BuildEmail(account, links.Url($"/reset-password/{Uri.EscapeDataString(token)}")));
    }

    public async Task<PasswordResetPreviewResponse> CheckAsync(string token, CancellationToken cancellationToken)
    {
        var reset = await RequireUsableAsync(token, cancellationToken);
        var account = await identity.FindByIdAsync(reset.UserId, cancellationToken)
            ?? throw new NotFoundException(InvalidLinkMessage);

        return new PasswordResetPreviewResponse(account.Email);
    }

    public async Task<CurrentUserResponse> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var reset = await RequireUsableAsync(request.Token, cancellationToken);

        // A password the rules refuse throws here, and the link stays usable for a second try.
        if (!await identity.ResetPasswordAsync(reset.UserId, request.Password, cancellationToken))
        {
            throw new NotFoundException(InvalidLinkMessage);
        }

        reset.Use(Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await identity.SignInAsync(reset.UserId, cancellationToken);

        return await identity.FindByIdAsync(reset.UserId, cancellationToken)
            ?? throw new NotFoundException(InvalidLinkMessage);
    }

    private async Task<PasswordResetToken> RequireUsableAsync(string token, CancellationToken cancellationToken)
    {
        if (!SecureToken.LooksValid(token))
        {
            throw new NotFoundException(InvalidLinkMessage);
        }

        var reset = await tokens.GetByTokenHashAsync(SecureToken.Hash(token), cancellationToken)
            ?? throw new NotFoundException(InvalidLinkMessage);

        if (reset.UsedAtUtc is not null)
        {
            throw new ConflictException("This link was already used. Ask for a new one if you need it.");
        }

        if (reset.CancelledAtUtc is not null)
        {
            throw new ConflictException("A newer link was sent. Use the latest email, or ask for a new one.");
        }

        if (!reset.IsUsableAt(Now()))
        {
            throw new ConflictException("This link has expired. Ask for a new one.");
        }

        return reset;
    }

    private static EmailMessage BuildEmail(CurrentUserResponse account, string link)
    {
        var greeting = string.IsNullOrWhiteSpace(account.FullName) ? "Hi," : $"Hi {account.FullName},";

        return new EmailMessage(
            account.Email,
            $"Reset your {AppInfo.Name} password",
            $"""
            {greeting}

            Someone asked to reset the password of your {AppInfo.Name} account ({account.Email}).

            Choose a new password here:
            {link}

            The link works once, for one hour. If you did not ask for this, ignore this email: your password stays as it is.
            """);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
