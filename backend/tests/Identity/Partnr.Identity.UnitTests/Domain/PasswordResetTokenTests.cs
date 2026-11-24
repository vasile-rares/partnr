using Partnr.Identity.Domain.Entities;
using Partnr.Identity.Domain.Exceptions;

namespace Partnr.Identity.UnitTests.Domain;

public class PasswordResetTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ANewLink_WorksForAnHour_AndNotAfter()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        Assert.True(token.IsUsableAt(Now.AddMinutes(59)));
        Assert.False(token.IsUsableAt(Now.AddHours(1)));
    }

    [Fact]
    public void ALink_WorksOnce()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        token.Use(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), token.UsedAtUtc);
        Assert.False(token.IsUsableAt(Now.AddMinutes(6)));
        Assert.Throws<DomainException>(() => token.Use(Now.AddMinutes(6)));
    }

    [Fact]
    public void ACancelledOrExpiredLink_CannotBeUsed()
    {
        var cancelled = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);
        cancelled.Cancel(Now.AddMinutes(1));
        var expired = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        Assert.Throws<DomainException>(() => cancelled.Use(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => expired.Use(Now.AddHours(2)));
    }

    [Fact]
    public void Cancel_KeepsTheFirstDate_AndLeavesAUsedLinkUsed()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);
        token.Cancel(Now.AddMinutes(1));
        token.Cancel(Now.AddMinutes(2));
        var used = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);
        used.Use(Now.AddMinutes(1));

        used.Cancel(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(1), token.CancelledAtUtc);
        Assert.Null(used.CancelledAtUtc);
    }

    [Fact]
    public void ItCoolsDown_ForAMinute_WhileItIsOpen()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        Assert.True(token.IsCoolingDownAt(Now.AddSeconds(59)));
        Assert.False(token.IsCoolingDownAt(Now.AddMinutes(1)));
    }

    [Fact]
    public void ALinkNeedsAnAccountAndAToken()
    {
        Assert.Throws<DomainException>(() => PasswordResetToken.Create(Guid.Empty, "hash", Now));
        Assert.Throws<DomainException>(() => PasswordResetToken.Create(Guid.NewGuid(), " ", Now));
    }
}
