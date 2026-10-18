using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Application;

public class PortalApprovalServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeProjectRepository _projects;
    private readonly FakeDeliverableRepository _deliverables = new();
    private readonly FakeApprovalRequestRepository _approvals = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _studio;
    private readonly Client _client;
    private readonly Project _project;
    private readonly Deliverable _deliverable;
    private readonly ApprovalRequest _round;
    private readonly PortalApprovalService _service;

    public PortalApprovalServiceTests()
    {
        _projects = new FakeProjectRepository { Clients = _clients };
        _studio = Workspace.Create("Studio Pop", Guid.NewGuid());
        _workspaces.Items.Add(_studio);
        _workspaces.ClientOf[_me] = [_studio.Id];
        _client = Client.Register(_studio.Id, "Elena", "elena@example.com");
        _client.LinkTo(_me, DateTime.UtcNow);
        _clients.Items.Add(_client);
        _currentUser.UserId = _me;

        _project = Project.Create(_studio.Id, _client.Id, "Website");
        _project.ChangeStatus(ProjectStatus.Active);
        _projects.Items.Add(_project);
        _deliverable = Deliverable.Create(_studio.Id, _project.Id, null, "Mockup", null, "https://example.com/file");
        _deliverable.Share();
        _deliverables.Items.Add(_deliverable);
        _round = ApprovalRequest.Request(_studio.Id, _project.Id, _deliverable.Id, Guid.NewGuid(), "Please check.");
        _approvals.Items.Add(_round);

        _service = new PortalApprovalService(_projects, _deliverables, _approvals, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork);
    }

    [Fact]
    public async Task Approve_ClosesTheRound_WithWhoDecided_AndAnOptionalMessage()
    {
        var response = await _service.ApproveAsync(
            _project.Id,
            _deliverable.Id,
            _round.Id,
            new ApproveRequest { Message = "  Looks great.  " },
            CancellationToken.None);

        Assert.Equal(ApprovalStatus.Approved, _round.Status);
        Assert.Equal(_me, _round.DecidedByUserId);
        Assert.Equal("Looks great.", _round.Message);
        Assert.Equal("Approved", response.Approval.Status);
        Assert.Null(response.Approval.Pending);
        Assert.Equal("Looks great.", Assert.Single(response.Approval.History).Message);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Approve_WithoutAMessage_IsAllowed()
    {
        await _service.ApproveAsync(_project.Id, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None);

        Assert.Equal(ApprovalStatus.Approved, _round.Status);
        Assert.Null(_round.Message);
    }

    [Fact]
    public async Task RequestChanges_ClosesTheRound_WithTheMessage()
    {
        var response = await _service.RequestChangesAsync(
            _project.Id,
            _deliverable.Id,
            _round.Id,
            new RequestChangesRequest { Message = "Bigger logo." },
            CancellationToken.None);

        Assert.Equal(ApprovalStatus.ChangesRequested, _round.Status);
        Assert.Equal("Bigger logo.", _round.Message);
        Assert.Equal("ChangesRequested", response.Approval.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestChanges_WithoutSayingWhat_Throws_AndKeepsTheRoundOpen(string message)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RequestChangesAsync(_project.Id, _deliverable.Id, _round.Id, new RequestChangesRequest { Message = message }, CancellationToken.None));

        Assert.True(_round.IsPending);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADecision_IsFinal_ASecondAnswerIsAConflict()
    {
        await _service.ApproveAsync(_project.Id, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RequestChangesAsync(_project.Id, _deliverable.Id, _round.Id, new RequestChangesRequest { Message = "Changed my mind." }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ApproveAsync(_project.Id, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None));

        Assert.Equal(ApprovalStatus.Approved, _round.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AWithdrawnRound_CannotBeAnswered()
    {
        _round.Withdraw();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ApproveAsync(_project.Id, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None));

        Assert.Equal(ApprovalStatus.Withdrawn, _round.Status);
    }

    [Fact]
    public async Task NothingIsFound_ForADraftProject_AnArchivedOne_OrSomeoneElses()
    {
        var draft = Project.Create(_studio.Id, _client.Id, "Draft");
        _projects.Items.Add(draft);
        var archived = Project.Create(_studio.Id, _client.Id, "Archived");
        archived.ChangeStatus(ProjectStatus.Active);
        archived.Archive();
        _projects.Items.Add(archived);
        var other = Client.Register(_studio.Id, "Someone", "someone@example.com");
        other.LinkTo(Guid.NewGuid(), DateTime.UtcNow);
        _clients.Items.Add(other);
        var theirs = Project.Create(_studio.Id, other.Id, "Theirs");
        theirs.ChangeStatus(ProjectStatus.Active);
        _projects.Items.Add(theirs);

        foreach (var projectId in new[] { draft.Id, archived.Id, theirs.Id, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.ApproveAsync(projectId, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None));
        }

        Assert.True(_round.IsPending);
    }

    [Fact]
    public async Task NothingIsFound_ForADeliverableTheClientCannotSee()
    {
        var draft = Deliverable.Create(_studio.Id, _project.Id, null, "Draft", null, "https://example.com/draft");
        _deliverables.Items.Add(draft);
        var draftRound = ApprovalRequest.Request(_studio.Id, _project.Id, draft.Id, Guid.NewGuid(), null);
        _approvals.Items.Add(draftRound);
        var archived = Deliverable.Create(_studio.Id, _project.Id, null, "Archived", null, "https://example.com/archived");
        archived.Share();
        archived.Archive();
        _deliverables.Items.Add(archived);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ApproveAsync(_project.Id, draft.Id, draftRound.Id, new ApproveRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ApproveAsync(_project.Id, archived.Id, Guid.NewGuid(), new ApproveRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ApproveAsync(_project.Id, Guid.NewGuid(), _round.Id, new ApproveRequest(), CancellationToken.None));

        Assert.True(draftRound.IsPending);
    }

    [Fact]
    public async Task NothingIsFound_ForARoundOfAnotherDeliverable_OrAnUnknownOne()
    {
        var other = Deliverable.Create(_studio.Id, _project.Id, null, "Other", null, "https://example.com/other");
        other.Share();
        _deliverables.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ApproveAsync(_project.Id, other.Id, _round.Id, new ApproveRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ApproveAsync(_project.Id, _deliverable.Id, Guid.NewGuid(), new ApproveRequest(), CancellationToken.None));

        Assert.True(_round.IsPending);
    }

    [Fact]
    public async Task Anonymous_ThrowsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.ApproveAsync(_project.Id, _deliverable.Id, _round.Id, new ApproveRequest(), CancellationToken.None));
    }
}
