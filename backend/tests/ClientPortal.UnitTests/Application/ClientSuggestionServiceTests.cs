using ClientPortal.Application.Authorization;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ClientSuggestionServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly Workspace _workspace;
    private readonly ClientSuggestionService _service;

    public ClientSuggestionServiceTests()
    {
        _workspace = Workspace.Create("Studio Pop", _me);
        _workspaces.Items.Add(_workspace);
        _service = new ClientSuggestionService(new WorkspaceAccess(_workspaces, new FakeCurrentUser(_me)), _identity, _clients);
    }

    [Fact]
    public async Task Suggest_FindsAccountsWhoseEmailStartsWithWhatWasTyped_WithTheirNames()
    {
        _identity.AddAccount("ana@acme.example", "correct horse", "Ana Pop");
        _identity.AddAccount("anca@acme.example", "correct horse", "Anca Ionescu");
        _identity.AddAccount("bob@acme.example", "correct horse", "Bob");

        var result = await _service.SuggestAsync(_workspace.Id, " AN ", CancellationToken.None);
        Assert.Empty(result); // two characters are too few, even with spaces around them

        result = await _service.SuggestAsync(_workspace.Id, " ANA ", CancellationToken.None);

        var found = Assert.Single(result);
        Assert.Equal("ana@acme.example", found.Email);
        Assert.Equal("Ana Pop", found.FullName);
        Assert.False(found.AlreadyClient);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("an")]
    public async Task Suggest_WithFewerThanThreeCharacters_ReturnsNothing_AndDoesNotSearch(string? typed)
    {
        _identity.AddAccount("ana@acme.example", "correct horse");

        Assert.Empty(await _service.SuggestAsync(_workspace.Id, typed, CancellationToken.None));
    }

    [Fact]
    public async Task Suggest_MatchesOnTheStartOfTheEmailOnly_NotTheNameOrTheMiddle()
    {
        _identity.AddAccount("someone@example.com", "correct horse", "Ana Pop");
        _identity.AddAccount("x.ana@example.com", "correct horse", "Xavier");

        Assert.Empty(await _service.SuggestAsync(_workspace.Id, "ana", CancellationToken.None));
    }

    [Fact]
    public async Task Suggest_ReturnsAtMostFiveAccounts_InEmailOrder()
    {
        foreach (var letter in "gfedcbah")
        {
            _identity.AddAccount($"team-{letter}@example.com", "correct horse");
        }

        var result = await _service.SuggestAsync(_workspace.Id, "team", CancellationToken.None);

        Assert.Equal(
            ["team-a@example.com", "team-b@example.com", "team-c@example.com", "team-d@example.com", "team-e@example.com"],
            result.Select(r => r.Email));
    }

    [Fact]
    public async Task Suggest_LeavesOutTheMembersOfTheWorkspace_AndStillFillsTheLimitFromOthers()
    {
        var member = _identity.AddAccount("team-a@example.com", "correct horse");
        _workspace.AddMember(member.Id, WorkspaceRole.Member);
        foreach (var letter in "bcdefg")
        {
            _identity.AddAccount($"team-{letter}@example.com", "correct horse");
        }

        var result = await _service.SuggestAsync(_workspace.Id, "team", CancellationToken.None);

        Assert.Equal(5, result.Count);
        Assert.DoesNotContain(result, r => r.Email == "team-a@example.com");
    }

    [Fact]
    public async Task Suggest_MarksTheAccountsThatAreAlreadyClientsOfTheWorkspace()
    {
        _identity.AddAccount("ana@acme.example", "correct horse");
        _identity.AddAccount("anca@acme.example", "correct horse");
        _clients.Items.Add(Client.Register(_workspace.Id, null, "ana@acme.example"));
        var other = Workspace.Create("Other", Guid.NewGuid());
        _workspaces.Items.Add(other);
        _clients.Items.Add(Client.Register(other.Id, null, "anca@acme.example"));

        var result = await _service.SuggestAsync(_workspace.Id, "ana", CancellationToken.None);
        Assert.True(Assert.Single(result).AlreadyClient);

        result = await _service.SuggestAsync(_workspace.Id, "anc", CancellationToken.None);
        Assert.False(Assert.Single(result).AlreadyClient); // a card in another workspace does not count
    }

    [Theory]
    [InlineData("%%%")]
    [InlineData("a_b")]
    public async Task Suggest_TreatsWildcardCharactersAsPlainText(string typed)
    {
        _identity.AddAccount("axb@example.com", "correct horse");
        _identity.AddAccount("abc@example.com", "correct horse");

        Assert.Empty(await _service.SuggestAsync(_workspace.Id, typed, CancellationToken.None));
    }

    [Fact]
    public async Task Suggest_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound_EvenForAShortQuery()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.SuggestAsync(someoneElses.Id, "a", CancellationToken.None));
    }
}
