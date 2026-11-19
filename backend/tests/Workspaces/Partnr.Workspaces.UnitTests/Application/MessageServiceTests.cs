using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.DTOs.Messages;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Messaging;
using Partnr.Workspaces.Application.Services;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.UnitTests.Application;

/// <summary>One service for both doors: a member of the firm and the client read and write the same conversation.</summary>
public class MessageServiceTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeProjectRepository _projects;
    private readonly FakeConversationReadRepository _reads = new();
    private readonly FakeMessageRepository _messages;
    private readonly FakeEventPublisher _events = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeTimeProvider _time = new();
    private readonly MessageService _service;

    private readonly CurrentUserResponse _ana;
    private readonly CurrentUserResponse _elena;
    private readonly Workspace _studio;
    private readonly Client _card;
    private readonly Project _website;

    public MessageServiceTests()
    {
        _projects = new FakeProjectRepository { Clients = _clients };
        _messages = new FakeMessageRepository(_reads);

        _ana = _identity.AddAccount("ana@example.com", "pw", "Ana");
        _elena = _identity.AddAccount("elena@example.com", "pw", "Elena");
        _studio = Workspace.Create("Studio Pop", _ana.Id);
        _workspaces.Items.Add(_studio);
        _workspaces.ClientOf[_elena.Id] = [_studio.Id];
        _card = Client.Register(_studio.Id, "Elena card", "elena@example.com");
        _card.LinkTo(_elena.Id, _time.Now.UtcDateTime);
        _clients.Items.Add(_card);
        _website = AddProject(_card, ProjectStatus.Active);

        var access = new WorkspaceAccess(_workspaces, _currentUser);
        var clientAccess = new ClientAccess(_projects, _workspaces);
        var threads = new ConversationThreads(_messages, _reads, _identity, _events, new FakeUnitOfWork(), _time);
        _service = new MessageService(_projects, _clients, threads, clientAccess, new CallerAccess(access, clientAccess));
    }

    private Project AddProject(Client card, ProjectStatus status)
    {
        var project = Project.Create(card.WorkspaceId, card.Id, "Website");
        project.ChangeStatus(status);
        _projects.Items.Add(project);

        return project;
    }

    private static SaveMessageRequest Say(string body) => new() { Body = body };

    [Fact]
    public async Task TheFirmSeesTheConversation_WithTheClientsName()
    {
        _currentUser.UserId = _ana.Id;

        var list = await _service.ListAsync(_studio.Id, CancellationToken.None);

        var summary = Assert.Single(list);
        Assert.Equal(_website.Id, summary.ProjectId);
        Assert.Equal("Elena", summary.ClientName); // the account's name wins over the card's
    }

    [Fact]
    public async Task TheClientSeesTheSameConversation_WithoutAClientName()
    {
        _currentUser.UserId = _elena.Id;

        var list = await _service.ListAsync(_studio.Id, CancellationToken.None);

        var summary = Assert.Single(list);
        Assert.Equal(_website.Id, summary.ProjectId);
        Assert.Null(summary.ClientName);
    }

    [Fact]
    public async Task AProjectStillInPlanning_HasNoConversation_ForEitherSide()
    {
        var draft = AddProject(_card, ProjectStatus.Planning);

        foreach (var user in new[] { _ana.Id, _elena.Id })
        {
            _currentUser.UserId = user;

            Assert.DoesNotContain(await _service.ListAsync(_studio.Id, CancellationToken.None), c => c.ProjectId == draft.Id);
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_studio.Id, draft.Id, null, CancellationToken.None));
        }
    }

    [Fact]
    public async Task AClientNeverSeesAnotherClientsConversation()
    {
        var other = Client.Register(_studio.Id, "Someone else", "other@example.com");
        other.LinkTo(_identity.AddAccount("other@example.com", "pw").Id, _time.Now.UtcDateTime);
        _clients.Items.Add(other);
        var theirs = AddProject(other, ProjectStatus.Active);
        _currentUser.UserId = _elena.Id;

        Assert.DoesNotContain(await _service.ListAsync(_studio.Id, CancellationToken.None), c => c.ProjectId == theirs.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_studio.Id, theirs.Id, null, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SendAsync(_studio.Id, theirs.Id, Say("hi"), CancellationToken.None));
    }

    [Fact]
    public async Task AProjectOfAnotherFirm_IsNotFound_EvenForAClientOfBoth()
    {
        var other = Workspace.Create("Other Firm", Guid.NewGuid());
        _workspaces.Items.Add(other);
        _workspaces.ClientOf[_elena.Id] = [_studio.Id, other.Id];
        var otherCard = Client.Register(other.Id, "Elena", "elena@example.com");
        otherCard.LinkTo(_elena.Id, _time.Now.UtcDateTime);
        _clients.Items.Add(otherCard);
        var elsewhere = AddProject(otherCard, ProjectStatus.Active);
        _currentUser.UserId = _elena.Id;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_studio.Id, elsewhere.Id, null, CancellationToken.None));
        Assert.NotNull(await _service.GetAsync(other.Id, elsewhere.Id, null, CancellationToken.None));
    }

    [Fact]
    public async Task SomeoneWhoIsNeitherAMemberNorAClient_GetsTheUsual404()
    {
        _currentUser.UserId = _identity.AddAccount("stranger@example.com", "pw").Id;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_studio.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UnreadCountAsync(_studio.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_studio.Id, _website.Id, null, CancellationToken.None));
    }

    [Fact]
    public async Task AClientWhoseCardWasArchived_NoLongerHasAnyConversation()
    {
        _card.Archive();
        _workspaces.ClientOf.Remove(_elena.Id);
        _currentUser.UserId = _elena.Id;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_studio.Id, CancellationToken.None));
    }

    [Fact]
    public async Task WhatTheClientWrites_IsMarkedAsFromTheClient_AndEveryoneWhoReadsIsTold()
    {
        _currentUser.UserId = _elena.Id;

        var sent = await _service.SendAsync(_studio.Id, _website.Id, Say("Hello!"), CancellationToken.None);

        Assert.True(sent.IsMine);
        Assert.True(sent.FromClient);
        var push = Assert.IsType<ConversationChanged>(Assert.Single(_events.Published));
        Assert.Equal(_website.Id, push.ProjectId);
        Assert.Contains(_ana.Id, push.UserIds);
        Assert.Contains(_elena.Id, push.UserIds);
    }

    [Fact]
    public async Task WhatTheFirmWrites_ReachesTheClientAsUnread_UntilTheyMarkItRead()
    {
        _currentUser.UserId = _ana.Id;
        var sent = await _service.SendAsync(_studio.Id, _website.Id, Say("Mockups are ready."), CancellationToken.None);
        Assert.False(sent.FromClient);

        _currentUser.UserId = _elena.Id;
        Assert.Equal(1, (await _service.UnreadCountAsync(_studio.Id, CancellationToken.None)).Unread);

        await _service.MarkReadAsync(_studio.Id, _website.Id, new MarkConversationReadRequest { MessageId = sent.Id }, CancellationToken.None);

        Assert.Equal(0, (await _service.UnreadCountAsync(_studio.Id, CancellationToken.None)).Unread);
    }

    [Fact]
    public async Task OnlyTheAuthorCanChangeAMessage_AndNotTheOtherSide()
    {
        _currentUser.UserId = _ana.Id;
        var sent = await _service.SendAsync(_studio.Id, _website.Id, Say("Draft."), CancellationToken.None);

        _currentUser.UserId = _elena.Id;
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.EditAsync(_studio.Id, _website.Id, sent.Id, Say("Changed by the client."), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.DeleteAsync(_studio.Id, _website.Id, sent.Id, CancellationToken.None));
    }
}
