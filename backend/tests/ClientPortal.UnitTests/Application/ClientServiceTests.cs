using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Mappings;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ClientServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeClientInvitationRepository _invitations = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeTimeProvider _time = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _service = new ClientService(
            _clients,
            _invitations,
            new ClientResponseBuilder(_identity, _invitations, _time),
            new WorkspaceAccess(_workspaces, new FakeCurrentUser(_me)),
            _unitOfWork,
            _time);
    }

    [Fact]
    public async Task Register_NewEmail_AddsClientToTheWorkspaceAndSavesOnce()
    {
        var response = await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "Contact@Acme.com" },
            CancellationToken.None);

        Assert.Equal("contact@acme.com", response.Email);
        var client = Assert.Single(_clients.Items);
        Assert.Equal(_workspace.Id, client.WorkspaceId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_ExistingEmailInTheSameWorkspace_ThrowsConflict_AndDoesNotSave()
    {
        _clients.Items.Add(Client.Register(_workspace.Id, "Acme", "contact@acme.com"));

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Other", Email = "CONTACT@acme.com" },
            CancellationToken.None));

        Assert.Single(_clients.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_EmailUsedInAnotherWorkspace_IsAllowed()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        _clients.Items.Add(Client.Register(other.Id, "Acme", "contact@acme.com"));

        await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com" },
            CancellationToken.None);

        Assert.Equal(2, _clients.Items.Count);
    }

    [Fact]
    public async Task Register_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound_AndDoesNotSave()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RegisterAsync(
            someoneElses.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com" },
            CancellationToken.None));

        Assert.Empty(_clients.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsClient()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.GetByIdAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.Equal(client.Id, response.Id);
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task GetById_ClientOfAnotherWorkspace_ThrowsNotFound_EvenWithItsId()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(_workspace.Id, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetById_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);
        var theirs = Client.Register(someoneElses.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(someoneElses.Id, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Register_KeepsTheOptionalDetails()
    {
        var response = await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com", Company = "Acme Group", Phone = "0700", Notes = " " },
            CancellationToken.None);

        Assert.Equal("Acme Group", response.Company);
        Assert.Equal("0700", response.Phone);
        Assert.Null(response.Notes);
    }

    [Fact]
    public async Task List_HidesArchivedClientsUnlessAskedFor_AndClientsOfOtherWorkspaces()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var active = Client.Register(_workspace.Id, "Beta", "beta@example.com");
        var archived = Client.Register(_workspace.Id, "Alpha", "alpha@example.com");
        archived.Archive();
        _clients.Items.AddRange([active, archived, Client.Register(other.Id, "Theirs", "theirs@example.com")]);

        var defaultList = await _service.ListAsync(_workspace.Id, includeArchived: false, CancellationToken.None);
        var fullList = await _service.ListAsync(_workspace.Id, includeArchived: true, CancellationToken.None);

        Assert.Equal(active.Id, Assert.Single(defaultList).Id);
        Assert.Equal(["Alpha", "Beta"], fullList.Select(c => c.Name));
    }

    [Fact]
    public async Task Update_ChangesTheDetailsAndSavesOnce()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme SRL", Email = "office@acme.com", Company = "Acme Group" },
            CancellationToken.None);

        Assert.Equal("Acme SRL", response.Name);
        Assert.Equal("office@acme.com", response.Email);
        Assert.Equal("Acme Group", response.Company);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingItsOwnEmail_IsNotAConflict()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme SRL", Email = "CONTACT@acme.com" },
            CancellationToken.None);

        Assert.Equal("Acme SRL", response.Name);
    }

    [Fact]
    public async Task Update_ToTheEmailOfAnArchivedClient_ThrowsConflict_AndDoesNotSave()
    {
        var archived = Client.Register(_workspace.Id, "Old", "old@acme.com");
        archived.Archive();
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.AddRange([archived, client]);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme", Email = "old@acme.com" },
            CancellationToken.None));

        Assert.Equal("contact@acme.com", client.Email);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ClientOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(
            _workspace.Id,
            theirs.Id,
            new UpdateClientRequest { Name = "Hijacked", Email = "x@example.com" },
            CancellationToken.None));

        Assert.Equal("Acme", theirs.Name);
    }

    [Fact]
    public async Task ArchiveThenRestore_TogglesTheArchivedDate()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var archived = await _service.ArchiveAsync(_workspace.Id, client.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Archive_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);
        var theirs = Client.Register(someoneElses.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ArchiveAsync(someoneElses.Id, theirs.Id, CancellationToken.None));

        Assert.False(theirs.IsArchived);
    }

    [Fact]
    public async Task ArchiveAndRestore_AsAMember_ThrowForbidden_AndChangeNothing()
    {
        var (workspace, client) = AWorkspaceWhereIAmOnlyAMember();
        var archived = Client.Register(workspace.Id, "Old", "old@acme.com");
        archived.Archive();
        _clients.Items.Add(archived);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ArchiveAsync(workspace.Id, client.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RestoreAsync(workspace.Id, archived.Id, CancellationToken.None));

        Assert.False(client.IsArchived);
        Assert.True(archived.IsArchived);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RegisterAndUpdate_AsAMember_AreAllowed()
    {
        var (workspace, client) = AWorkspaceWhereIAmOnlyAMember();

        await _service.RegisterAsync(
            workspace.Id,
            new RegisterClientRequest { Name = "New", Email = "new@acme.com" },
            CancellationToken.None);
        var updated = await _service.UpdateAsync(
            workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Renamed", Email = "contact@acme.com" },
            CancellationToken.None);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(2, _clients.Items.Count);
    }

    private (Workspace Workspace, Client Client) AWorkspaceWhereIAmOnlyAMember()
    {
        var workspace = Workspace.Create("Someone else's studio", Guid.NewGuid());
        workspace.AddMember(_me, WorkspaceRole.Member);
        _workspaces.Items.Add(workspace);
        var client = Client.Register(workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        return (workspace, client);
    }

    private ClientInvitation AddPendingInvitation(Client client)
    {
        var invitation = ClientInvitation.Create(
            client.WorkspaceId, client.Id, client.Email, "hash-" + Guid.NewGuid(), _me, _time.Now.UtcDateTime);
        _invitations.Items.Add(invitation);

        return invitation;
    }

    [Fact]
    public async Task Update_WhileAnInvitationIsPending_RefusesToChangeTheDetails_ButTheNotesGoThrough()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com", "Acme Group", "0700", "old note");
        _clients.Items.Add(client);
        AddPendingInvitation(client);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Renamed", Email = "contact@acme.com", Company = "Acme Group", Phone = "0700", Notes = "x" },
            CancellationToken.None));
        Assert.Equal("Acme", client.Name);
        Assert.Equal("old note", client.Notes);

        // The form sends the unchanged details back together with the new notes.
        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme", Email = "CONTACT@acme.com", Company = "Acme Group", Phone = " 0700 ", Notes = "new note" },
            CancellationToken.None);

        Assert.Equal("new note", response.Notes);
        Assert.Equal("Acme", response.Name);
        Assert.True(response.DetailsLocked);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("company")]
    public async Task Update_WhileAnInvitationIsPending_RefusesEveryIdentityField(string field)
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com", "Acme Group", "0700");
        _clients.Items.Add(client);
        AddPendingInvitation(client);

        var request = new UpdateClientRequest
        {
            Name = "Acme",
            Email = field == "email" ? "other@acme.com" : "contact@acme.com",
            Company = field == "company" ? "Other" : "Acme Group",
            Phone = field == "phone" ? "999" : "0700",
        };

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(_workspace.Id, client.Id, request, CancellationToken.None));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AfterTheInvitationExpired_EditsEverythingAgain()
    {
        var client = Client.Register(_workspace.Id, "Acme", "wrong@acme.com");
        _clients.Items.Add(client);
        AddPendingInvitation(client);
        _time.Now = _time.Now.AddDays(8);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme", Email = "right@acme.com" },
            CancellationToken.None);

        Assert.Equal("right@acme.com", response.Email);
        Assert.False(response.DetailsLocked);
        Assert.Equal("Expired", response.Portal.Status);
    }

    [Fact]
    public async Task ALinkedClient_ShowsTheAccountsDetails_NotTheCards()
    {
        var account = _identity.AddAccount("elena@newjob.example", "correct horse", "Elena Ionescu");
        await _identity.UpdateProfileAsync(account.Id, "Elena Ionescu", "0722", "Newjob SRL", CancellationToken.None);
        var client = Client.Register(_workspace.Id, "Elena (card)", "elena@oldjob.example", "Oldjob", "000", "Prefers calls.");
        client.LinkTo(account.Id, _time.Now.UtcDateTime);
        _clients.Items.Add(client);

        var response = await _service.GetByIdAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.Equal("Elena Ionescu", response.Name);
        Assert.Equal("elena@newjob.example", response.Email);
        Assert.Equal("0722", response.Phone);
        Assert.Equal("Newjob SRL", response.Company);
        Assert.Equal("Prefers calls.", response.Notes);
        Assert.Equal("Active", response.Portal.Status);
        Assert.True(response.DetailsLocked);
    }

    [Fact]
    public async Task ALinkedClient_OnlyTheNotesCanBeEdited()
    {
        var account = _identity.AddAccount("elena@acme.com", "correct horse", "Elena Ionescu");
        var client = Client.Register(_workspace.Id, "Elena (card)", "elena@acme.com");
        client.LinkTo(account.Id, _time.Now.UtcDateTime);
        _clients.Items.Add(client);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Elena (card)", Email = "elena@acme.com" },
            CancellationToken.None));

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Elena Ionescu", Email = "elena@acme.com", Notes = "Great client." },
            CancellationToken.None);

        Assert.Equal("Great client.", response.Notes);
        Assert.Equal("Elena Ionescu", response.Name);
        Assert.Equal("Elena (card)", client.Name); // the card itself is not rewritten
    }

    [Fact]
    public async Task List_ShowsThePortalStatusOfEachClient()
    {
        var notInvited = Client.Register(_workspace.Id, "A one", "a@example.com");
        var invited = Client.Register(_workspace.Id, "B two", "b@example.com");
        var expired = Client.Register(_workspace.Id, "C three", "c@example.com");
        var active = Client.Register(_workspace.Id, "D four", "d@example.com");
        active.LinkTo(_identity.AddAccount("d@example.com", "correct horse").Id, _time.Now.UtcDateTime);
        _clients.Items.AddRange([notInvited, invited, expired, active]);
        AddPendingInvitation(invited);
        _invitations.Items.Add(ClientInvitation.Create(
            _workspace.Id, expired.Id, expired.Email, "hash-expired", _me, _time.Now.UtcDateTime.AddDays(-8)));

        var list = await _service.ListAsync(_workspace.Id, includeArchived: false, CancellationToken.None);

        Assert.Equal(["NotInvited", "Invited", "Expired", "Active"], list.Select(c => c.Portal.Status));
        Assert.Equal([false, true, false, true], list.Select(c => c.DetailsLocked));
    }

    [Fact]
    public async Task Archive_CancelsThePendingInvitation_SoItsLinkStopsWorking()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);
        var invitation = AddPendingInvitation(client);

        var response = await _service.ArchiveAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.NotNull(invitation.CancelledAtUtc);
        Assert.Equal("NotInvited", response.Portal.Status);
        Assert.False(response.DetailsLocked);
    }

    [Fact]
    public async Task Archive_ALinkedClient_KeepsTheLinkToTheAccount()
    {
        var accountId = _identity.AddAccount("elena@acme.com", "correct horse").Id;
        var client = Client.Register(_workspace.Id, "Elena", "elena@acme.com");
        client.LinkTo(accountId, _time.Now.UtcDateTime);
        _clients.Items.Add(client);

        await _service.ArchiveAsync(_workspace.Id, client.Id, CancellationToken.None);
        await _service.RestoreAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.Equal(accountId, client.UserId);
    }
}
