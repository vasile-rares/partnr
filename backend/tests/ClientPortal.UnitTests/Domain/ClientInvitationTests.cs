using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ClientInvitationTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    private static ClientInvitation NewInvitation() =>
        ClientInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), "  Elena@Acme.COM ", "hash", Guid.NewGuid(), Now);

    [Fact]
    public void Create_NormalizesTheEmail_AndExpiresAfterSevenDays()
    {
        var invitation = NewInvitation();

        Assert.Equal("elena@acme.com", invitation.Email);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAtUtc);
        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(Now));
        Assert.True(invitation.IsOpen);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "elena@acme.com", "hash")]
    [InlineData("11111111-1111-1111-1111-111111111111", " ", "hash")]
    [InlineData("11111111-1111-1111-1111-111111111111", "elena@acme.com", "")]
    public void Create_WithMissingData_Throws(string workspaceId, string email, string tokenHash) =>
        Assert.Throws<DomainException>(() =>
            ClientInvitation.Create(Guid.Parse(workspaceId), Guid.NewGuid(), email, tokenHash, Guid.NewGuid(), Now));

    [Fact]
    public void Create_WithoutSender_Throws() =>
        Assert.Throws<DomainException>(() =>
            ClientInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), "elena@acme.com", "hash", Guid.Empty, Now));

    [Fact]
    public void StatusAt_BecomesExpiredAtTheExpiryInstant_AndStaysOpen()
    {
        var invitation = NewInvitation();

        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(Now.AddDays(7).AddTicks(-1)));
        Assert.Equal(InvitationStatus.Expired, invitation.StatusAt(Now.AddDays(7)));
        Assert.True(invitation.IsOpen);
        Assert.False(invitation.IsPending(Now.AddDays(8)));
    }

    [Fact]
    public void Accept_MarksItAccepted_AndOnlyOnce()
    {
        var invitation = NewInvitation();

        invitation.Accept(Now.AddHours(1));

        Assert.Equal(InvitationStatus.Accepted, invitation.StatusAt(Now.AddHours(2)));
        Assert.False(invitation.IsOpen);
        Assert.Throws<DomainException>(() => invitation.Accept(Now.AddHours(2)));
    }

    [Fact]
    public void Accept_AfterExpiryOrCancel_Throws()
    {
        var expired = NewInvitation();
        var cancelled = NewInvitation();
        cancelled.Cancel(Now);

        Assert.Throws<DomainException>(() => expired.Accept(Now.AddDays(8)));
        Assert.Throws<DomainException>(() => cancelled.Accept(Now.AddHours(1)));
    }

    [Fact]
    public void Cancel_KeepsTheFirstDate_AndWorksOnAnExpiredInvitation()
    {
        var invitation = NewInvitation();

        invitation.Cancel(Now.AddDays(8));
        invitation.Cancel(Now.AddDays(9));

        Assert.Equal(Now.AddDays(8), invitation.CancelledAtUtc);
        Assert.Equal(InvitationStatus.Cancelled, invitation.StatusAt(Now.AddDays(10)));
        Assert.False(invitation.IsOpen);
    }

    [Fact]
    public void Cancel_AnAcceptedInvitation_Throws()
    {
        var invitation = NewInvitation();
        invitation.Accept(Now);

        Assert.Throws<DomainException>(() => invitation.Cancel(Now.AddHours(1)));
    }
}
