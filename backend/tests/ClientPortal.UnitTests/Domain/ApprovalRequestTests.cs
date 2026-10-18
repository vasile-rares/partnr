using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ApprovalRequestTests
{
    private static ApprovalRequest NewRequest(string? note = null) =>
        ApprovalRequest.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), note);

    [Fact]
    public void Request_StartsPending_AndTrimsTheNote()
    {
        var request = NewRequest("  Please check the mobile layout.  ");

        Assert.Equal(ApprovalStatus.Pending, request.Status);
        Assert.True(request.IsPending);
        Assert.Equal("Please check the mobile layout.", request.Note);
        Assert.Null(request.ResolvedAtUtc);
        Assert.Null(request.DecidedByUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_WithoutANote_IsAllowed_AndStoresNull(string? note) =>
        Assert.Null(NewRequest(note).Note);

    [Fact]
    public void Request_WithoutTheIds_Throws()
    {
        Assert.Throws<DomainException>(() => ApprovalRequest.Request(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null));
        Assert.Throws<DomainException>(() => ApprovalRequest.Request(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null));
        Assert.Throws<DomainException>(() => ApprovalRequest.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null));
        Assert.Throws<DomainException>(() => ApprovalRequest.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null));
    }

    [Fact]
    public void Approve_RecordsWhoDecidedAndWhen_WithAnOptionalMessage()
    {
        var request = NewRequest();
        var client = Guid.NewGuid();

        request.Approve(client, "  Looks great.  ");

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.False(request.IsPending);
        Assert.Equal(client, request.DecidedByUserId);
        Assert.Equal("Looks great.", request.Message);
        Assert.NotNull(request.ResolvedAtUtc);
    }

    [Fact]
    public void Approve_WithoutAMessage_IsAllowed()
    {
        var request = NewRequest();

        request.Approve(Guid.NewGuid(), null);

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Null(request.Message);
    }

    [Fact]
    public void RequestChanges_NeedsAMessage()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => request.RequestChanges(Guid.NewGuid(), null));
        Assert.Throws<DomainException>(() => request.RequestChanges(Guid.NewGuid(), "   "));

        Assert.True(request.IsPending);
    }

    [Fact]
    public void RequestChanges_RecordsTheMessage()
    {
        var request = NewRequest();

        request.RequestChanges(Guid.NewGuid(), "The logo is too small.");

        Assert.Equal(ApprovalStatus.ChangesRequested, request.Status);
        Assert.Equal("The logo is too small.", request.Message);
        Assert.NotNull(request.ResolvedAtUtc);
    }

    [Fact]
    public void Decide_WithoutKnowingWho_Throws()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => request.Approve(Guid.Empty, null));
        Assert.True(request.IsPending);
    }

    [Fact]
    public void Withdraw_ClosesTheRound_WithoutADecider()
    {
        var request = NewRequest();

        request.Withdraw();

        Assert.Equal(ApprovalStatus.Withdrawn, request.Status);
        Assert.Null(request.DecidedByUserId);
        Assert.NotNull(request.ResolvedAtUtc);
    }

    [Fact]
    public void AClosedRound_CannotBeDecidedOrWithdrawnAgain()
    {
        var approved = NewRequest();
        approved.Approve(Guid.NewGuid(), null);
        var withdrawn = NewRequest();
        withdrawn.Withdraw();

        Assert.Throws<DomainException>(() => approved.RequestChanges(Guid.NewGuid(), "Actually no."));
        Assert.Throws<DomainException>(() => approved.Withdraw());
        Assert.Throws<DomainException>(() => withdrawn.Approve(Guid.NewGuid(), null));
        Assert.Throws<DomainException>(() => withdrawn.Withdraw());

        Assert.Equal(ApprovalStatus.Approved, approved.Status);
        Assert.Equal(ApprovalStatus.Withdrawn, withdrawn.Status);
    }
}
