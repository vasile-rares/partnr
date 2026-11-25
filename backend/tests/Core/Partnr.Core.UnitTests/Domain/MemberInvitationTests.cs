using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;
using Partnr.Core.Domain.Exceptions;

namespace Partnr.Core.UnitTests.Domain;

public class MemberInvitationTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static MemberInvitation New() => MemberInvitation.Create(WorkspaceId, "  Mihai@Studio.Example ", "a-hash", Owner, Now);

    [Fact]
    public void Create_NormalizesTheEmail_AndIsPendingForSevenDays()
    {
        var invitation = New();

        Assert.Equal("mihai@studio.example", invitation.Email);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAtUtc);
        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(Now));
        Assert.True(invitation.IsOpen);
    }

    [Fact]
    public void Create_WithoutWhatItNeeds_Throws()
    {
        Assert.Throws<DomainException>(() => MemberInvitation.Create(Guid.Empty, "a@b.example", "h", Owner, Now));
        Assert.Throws<DomainException>(() => MemberInvitation.Create(WorkspaceId, "a@b.example", "h", Guid.Empty, Now));
        Assert.Throws<DomainException>(() => MemberInvitation.Create(WorkspaceId, " ", "h", Owner, Now));
        Assert.Throws<DomainException>(() => MemberInvitation.Create(WorkspaceId, "a@b.example", " ", Owner, Now));
    }

    [Fact]
    public void ItExpiresOnItsOwn_ButStaysOpenUntilReplacedOrUsed()
    {
        var invitation = New();

        Assert.Equal(InvitationStatus.Expired, invitation.StatusAt(Now.AddDays(7)));
        Assert.True(invitation.IsOpen);
    }

    [Fact]
    public void Accept_MarksItUsed_AndOnlyOnce()
    {
        var invitation = New();

        invitation.Accept(Now.AddHours(1));

        Assert.Equal(InvitationStatus.Accepted, invitation.StatusAt(Now.AddHours(2)));
        Assert.False(invitation.IsOpen);
        Assert.Throws<DomainException>(() => invitation.Accept(Now.AddHours(2)));
    }

    [Fact]
    public void Accept_AfterExpiryOrCancellation_Throws()
    {
        var expired = New();
        Assert.Throws<DomainException>(() => expired.Accept(Now.AddDays(8)));

        var cancelled = New();
        cancelled.Cancel(Now);
        Assert.Throws<DomainException>(() => cancelled.Accept(Now.AddMinutes(1)));
    }

    [Fact]
    public void Cancel_Twice_KeepsTheFirstDate_ButAnAcceptedOneCannotBeCancelled()
    {
        var invitation = New();
        invitation.Cancel(Now);
        invitation.Cancel(Now.AddHours(1));
        Assert.Equal(Now, invitation.CancelledAtUtc);

        var accepted = New();
        accepted.Accept(Now);
        Assert.Throws<DomainException>(() => accepted.Cancel(Now.AddHours(1)));
    }

    [Fact]
    public void TheTokenIsHashedTheSameWayAsAClientInvitations()
    {
        var (token, hash) = MemberInvitation.NewToken();

        Assert.Equal(hash, MemberInvitation.HashToken(token));
        Assert.Equal(hash, ClientInvitation.HashToken(token));
        Assert.DoesNotContain(token, hash);
    }
}
