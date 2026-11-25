using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.UnitTests.Application;

public class MemberServiceTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _owner;
    private readonly Guid _mihai;
    private readonly Guid _dana;
    private readonly Workspace _workspace;
    private readonly MemberService _service;

    public MemberServiceTests()
    {
        _owner = _identity.AddAccount("ana@studio.example", "pw", "Ana Pop").Id;
        _mihai = _identity.AddAccount("mihai@studio.example", "pw", "Mihai Member").Id;
        _dana = _identity.AddAccount("dana@studio.example", "pw", " ").Id;
        _workspace = Workspace.Create("Studio Pop", _owner);
        _workspace.AddMember(_mihai, WorkspaceRole.Member);
        _workspace.AddMember(_dana, WorkspaceRole.Member);
        _workspaces.Items.Add(_workspace);
        _currentUser.UserId = _owner;

        _service = new MemberService(_identity, _meetings, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork, _time);
    }

    [Fact]
    public async Task List_ShowsTheOwnerFirst_WithNamesAndRoles_AndTheEmailWhenThereIsNoName()
    {
        _currentUser.UserId = _mihai;

        var members = await _service.ListAsync(_workspace.Id, CancellationToken.None);

        Assert.Equal(["Ana Pop", "Mihai Member", "dana@studio.example"], members.Select(m => m.Name));
        Assert.Equal(["Owner", "Member", "Member"], members.Select(m => m.Role));
        Assert.Equal("ana@studio.example", members[0].Email);
    }

    [Fact]
    public async Task List_ForSomeoneOutsideTheWorkspace_IsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_workspace.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Remove_TakesTheMemberOut_AndSavesOnce()
    {
        await _service.RemoveAsync(_workspace.Id, _mihai, CancellationToken.None);

        Assert.Null(_workspace.RoleOf(_mihai));
        Assert.Equal(WorkspaceRole.Member, _workspace.RoleOf(_dana));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Remove_AsAMember_IsForbidden_AndAsAStranger_NotFound()
    {
        _currentUser.UserId = _mihai;
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.RemoveAsync(_workspace.Id, _dana, CancellationToken.None));

        _currentUser.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RemoveAsync(_workspace.Id, _dana, CancellationToken.None));

        Assert.Equal(WorkspaceRole.Member, _workspace.RoleOf(_dana));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Remove_TheOwner_IsAConflict_AndSomeoneWhoIsNotAMemberNotFound()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _service.RemoveAsync(_workspace.Id, _owner, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RemoveAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(WorkspaceRole.Owner, _workspace.RoleOf(_owner));
    }

    [Fact]
    public async Task Remove_AMemberWhoOrganisesUpcomingMeetings_IsAConflictThatSaysHowMany()
    {
        _meetings.Items.Add(MeetingOf(_mihai, startsInDays: 2));
        _meetings.Items.Add(MeetingOf(_mihai, startsInDays: 5));

        var error = await Assert.ThrowsAsync<ConflictException>(() => _service.RemoveAsync(_workspace.Id, _mihai, CancellationToken.None));

        Assert.Contains("Mihai Member", error.Message);
        Assert.Contains("2 upcoming meetings", error.Message);
        Assert.Equal(WorkspaceRole.Member, _workspace.RoleOf(_mihai));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Remove_IgnoresMeetingsThatAreCancelled_OverOrSomeoneElses()
    {
        var cancelled = MeetingOf(_mihai, startsInDays: 2);
        cancelled.Cancel(_time.Now.UtcDateTime);
        _meetings.Items.Add(cancelled);
        _meetings.Items.Add(MeetingOf(_mihai, startsInDays: -3));
        _meetings.Items.Add(MeetingOf(_dana, startsInDays: 2));

        await _service.RemoveAsync(_workspace.Id, _mihai, CancellationToken.None);

        Assert.Null(_workspace.RoleOf(_mihai));
    }

    private Meeting MeetingOf(Guid organizer, int startsInDays)
    {
        var start = _time.Now.UtcDateTime.AddDays(startsInDays);

        // Created an hour before it starts, so a meeting in the past can be made too.
        return Meeting.Schedule(_workspace.Id, Guid.NewGuid(), organizer, null, "Call", null, start, start.AddHours(1), start.AddHours(-1));
    }
}
