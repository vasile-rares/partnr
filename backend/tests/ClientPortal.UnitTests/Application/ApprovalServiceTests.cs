using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Mappings;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ApprovalServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeApprovalRequestRepository _approvals = new();
    private readonly FakeDeliverableRepository _deliverables = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly Project _project;
    private readonly ApprovalService _service;

    public ApprovalServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _project = Project.Create(_workspace.Id, Guid.NewGuid(), "Website");
        _projects.Items.Add(_project);
        _currentUser = new FakeCurrentUser(_me);

        _service = new ApprovalService(
            _approvals,
            _deliverables,
            _projects,
            new DeliverableResponseBuilder(_approvals),
            new WorkspaceAccess(_workspaces, _currentUser),
            _unitOfWork);
    }

    [Fact]
    public async Task Request_OpensAPendingRound_WithTheNote_AndWhoAsked()
    {
        var deliverable = AddDeliverable(shared: true);

        var response = await _service.RequestAsync(
            _workspace.Id,
            _project.Id,
            deliverable.Id,
            new RequestApprovalRequest { Note = "  Check the mobile layout.  " },
            CancellationToken.None);

        var round = Assert.Single(_approvals.Items);
        Assert.Equal(ApprovalStatus.Pending, round.Status);
        Assert.Equal("Check the mobile layout.", round.Note);
        Assert.Equal(_me, round.RequestedByUserId);
        Assert.Equal("Pending", response.ApprovalStatus);
        Assert.Equal(round.Id, Assert.Single(response.Approvals).Id);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Request_WithoutANote_IsAllowed()
    {
        var deliverable = AddDeliverable(shared: true);

        await _service.RequestAsync(_workspace.Id, _project.Id, deliverable.Id, new RequestApprovalRequest(), CancellationToken.None);

        Assert.Null(Assert.Single(_approvals.Items).Note);
    }

    [Fact]
    public async Task Request_OnADraft_ThrowsConflict_BecauseTheClientCannotSeeIt()
    {
        var deliverable = AddDeliverable(shared: false);

        await Assert.ThrowsAsync<ConflictException>(() => Request(deliverable));

        Assert.Empty(_approvals.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Request_WhileOneIsPending_ThrowsConflict()
    {
        var deliverable = AddDeliverable(shared: true);
        await Request(deliverable);

        await Assert.ThrowsAsync<ConflictException>(() => Request(deliverable));

        Assert.Single(_approvals.Items);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("changes")]
    [InlineData("withdraw")]
    public async Task Request_AfterTheRoundClosed_OpensANewRound_AndKeepsTheOldOne(string how)
    {
        var deliverable = AddDeliverable(shared: true);
        var first = ApprovalRequest.Request(_workspace.Id, _project.Id, deliverable.Id, _me, "First");
        _approvals.Items.Add(first);
        switch (how)
        {
            case "approve":
                first.Approve(Guid.NewGuid(), null);
                break;
            case "changes":
                first.RequestChanges(Guid.NewGuid(), "Bigger logo.");
                break;
            default:
                first.Withdraw();
                break;
        }

        var response = await Request(deliverable);

        Assert.Equal(2, _approvals.Items.Count);
        Assert.Equal("Pending", response.ApprovalStatus);
        Assert.Equal(2, response.Approvals.Count);
    }

    [Fact]
    public async Task Request_OnAnArchivedDeliverable_ThrowsConflict()
    {
        var deliverable = AddDeliverable(shared: true);
        deliverable.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => Request(deliverable));
    }

    [Fact]
    public async Task Request_InAnArchivedProject_ThrowsConflict()
    {
        var deliverable = AddDeliverable(shared: true);
        _project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => Request(deliverable));
    }

    [Fact]
    public async Task Request_ForADeliverableOfAnotherProject_ThrowsNotFound()
    {
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        var theirs = Deliverable.Create(_workspace.Id, other.Id, null, "Theirs", null, "https://example.com");
        theirs.Share();
        _deliverables.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RequestAsync(_workspace.Id, _project.Id, theirs.Id, new RequestApprovalRequest(), CancellationToken.None));

        Assert.Empty(_approvals.Items);
    }

    [Fact]
    public async Task Request_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        var deliverable = AddDeliverable(shared: true);
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => Request(deliverable));

        Assert.Empty(_approvals.Items);
    }

    [Fact]
    public async Task Request_AsAMember_IsAllowed()
    {
        var member = Guid.NewGuid();
        _workspace.AddMember(member, WorkspaceRole.Member);
        var deliverable = AddDeliverable(shared: true);
        _currentUser.UserId = member;

        await Request(deliverable);

        Assert.Equal(member, Assert.Single(_approvals.Items).RequestedByUserId);
    }

    [Fact]
    public async Task Withdraw_ClosesThePendingRound_AndTheStatusGoesBackToWhatStoodBefore()
    {
        var deliverable = AddDeliverable(shared: true);
        var approved = ApprovalRequest.Request(_workspace.Id, _project.Id, deliverable.Id, _me, null);
        approved.Approve(Guid.NewGuid(), null);
        _approvals.Items.Add(approved);
        var pending = (await Request(deliverable)).Approvals[0];

        var response = await _service.WithdrawAsync(_workspace.Id, _project.Id, deliverable.Id, pending.Id, CancellationToken.None);

        Assert.Equal("Approved", response.ApprovalStatus);
        Assert.Equal("Withdrawn", response.Approvals[0].Status);
        Assert.NotNull(response.Approvals[0].ResolvedAtUtc);
    }

    [Fact]
    public async Task Withdraw_AClosedRound_ThrowsConflict()
    {
        var deliverable = AddDeliverable(shared: true);
        var round = ApprovalRequest.Request(_workspace.Id, _project.Id, deliverable.Id, _me, null);
        round.Approve(Guid.NewGuid(), null);
        _approvals.Items.Add(round);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.WithdrawAsync(_workspace.Id, _project.Id, deliverable.Id, round.Id, CancellationToken.None));

        Assert.Equal(ApprovalStatus.Approved, round.Status);
    }

    [Fact]
    public async Task Withdraw_ARoundOfAnotherDeliverable_ThrowsNotFound_AndLeavesItOpen()
    {
        var mine = AddDeliverable(shared: true);
        var theirs = AddDeliverable(shared: true);
        var theirRound = ApprovalRequest.Request(_workspace.Id, _project.Id, theirs.Id, _me, null);
        _approvals.Items.Add(theirRound);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.WithdrawAsync(_workspace.Id, _project.Id, mine.Id, theirRound.Id, CancellationToken.None));

        Assert.True(theirRound.IsPending);
    }

    [Fact]
    public async Task Withdraw_InAnArchivedProject_ThrowsConflict()
    {
        var deliverable = AddDeliverable(shared: true);
        var round = ApprovalRequest.Request(_workspace.Id, _project.Id, deliverable.Id, _me, null);
        _approvals.Items.Add(round);
        _project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.WithdrawAsync(_workspace.Id, _project.Id, deliverable.Id, round.Id, CancellationToken.None));

        Assert.True(round.IsPending);
    }

    private Task<ClientPortal.Application.DTOs.Deliverables.DeliverableResponse> Request(Deliverable deliverable) =>
        _service.RequestAsync(_workspace.Id, _project.Id, deliverable.Id, new RequestApprovalRequest(), CancellationToken.None);

    private Deliverable AddDeliverable(bool shared)
    {
        var deliverable = Deliverable.Create(_workspace.Id, _project.Id, null, "Mockup", null, "https://figma.com/file/abc");
        if (shared)
        {
            deliverable.Share();
        }

        _deliverables.Items.Add(deliverable);

        return deliverable;
    }
}
