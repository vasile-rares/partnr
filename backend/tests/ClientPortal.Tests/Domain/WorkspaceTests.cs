using ClientPortal.Domain.Common;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Tests.Domain;

public class WorkspaceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_TrimsNameAndMakesTheCreatorTheOnlyOwner()
    {
        var workspace = Workspace.Create("  Studio Pop ", OwnerId);

        Assert.Equal("Studio Pop", workspace.Name);
        Assert.NotEqual(Guid.Empty, workspace.Id);
        var member = Assert.Single(workspace.Members);
        Assert.Equal(OwnerId, member.UserId);
        Assert.Equal(workspace.Id, member.WorkspaceId);
        Assert.Equal(WorkspaceRole.Owner, member.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name) =>
        Assert.Throws<DomainException>(() => Workspace.Create(name, OwnerId));

    [Fact]
    public void Create_WithTooLongName_Throws() =>
        Assert.Throws<DomainException>(() => Workspace.Create(new string('a', Workspace.NameMaxLength + 1), OwnerId));

    [Fact]
    public void Create_WithoutOwner_Throws() =>
        Assert.Throws<DomainException>(() => Workspace.Create("Studio", Guid.Empty));

    [Fact]
    public void Rename_ChangesTheName()
    {
        var workspace = Workspace.Create("Studio", OwnerId);

        workspace.Rename(" Studio Pop SRL ");

        Assert.Equal("Studio Pop SRL", workspace.Name);
    }

    [Fact]
    public void Rename_ToBlank_Throws()
    {
        var workspace = Workspace.Create("Studio", OwnerId);

        Assert.Throws<DomainException>(() => workspace.Rename(" "));
        Assert.Equal("Studio", workspace.Name);
    }

    [Fact]
    public void AddMember_GivesTheUserTheRole()
    {
        var workspace = Workspace.Create("Studio", OwnerId);
        var userId = Guid.NewGuid();

        workspace.AddMember(userId, WorkspaceRole.Member);

        Assert.Equal(WorkspaceRole.Member, workspace.RoleOf(userId));
        Assert.Equal(2, workspace.Members.Count);
    }

    [Fact]
    public void AddMember_Twice_Throws()
    {
        var workspace = Workspace.Create("Studio", OwnerId);

        Assert.Throws<DomainException>(() => workspace.AddMember(OwnerId, WorkspaceRole.Member));
        Assert.Equal(WorkspaceRole.Owner, workspace.RoleOf(OwnerId));
    }

    [Fact]
    public void RoleOf_ReturnsTheRoleOrNullForStrangers()
    {
        var workspace = Workspace.Create("Studio", OwnerId);

        Assert.Equal(WorkspaceRole.Owner, workspace.RoleOf(OwnerId));
        Assert.Null(workspace.RoleOf(Guid.NewGuid()));
    }
}
