using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Exceptions;

namespace Partnr.Core.UnitTests.Domain;

public class MessageTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Author = Guid.NewGuid();

    [Fact]
    public void Write_TrimsTheEnds_ButKeepsLineBreaks()
    {
        var message = Write("  First line.\nSecond line.  ");

        Assert.Equal("First line.\nSecond line.", message.Body);
        Assert.Equal(Now, message.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Write_NeedsSomeText(string body) =>
        Assert.Throws<DomainException>(() => Write(body));

    [Fact]
    public void Write_RefusesAnythingLongerThanTheLimit()
    {
        Assert.Throws<DomainException>(() => Write(new string('x', Message.BodyMaxLength + 1)));
        Assert.Equal(Message.BodyMaxLength, Write(new string('x', Message.BodyMaxLength)).Body.Length);
    }

    [Fact]
    public void TheAuthor_CanEditWithinTenMinutes_AndItSaysSo()
    {
        var message = Write("Helo");

        message.Edit(Author, "Hello", Now.AddMinutes(9));

        Assert.Equal("Hello", message.Body);
        Assert.Equal(Now.AddMinutes(9), message.EditedAtUtc);
    }

    [Fact]
    public void AfterTenMinutes_NothingCanBeChanged()
    {
        var message = Write("Hello");

        Assert.False(message.CanBeChangedBy(Author, Now.AddMinutes(10)));
        Assert.Throws<DomainException>(() => message.Edit(Author, "Changed", Now.AddMinutes(10)));
        Assert.Throws<DomainException>(() => message.Delete(Author, Now.AddMinutes(10)));
    }

    [Fact]
    public void SomeoneElse_CannotChangeIt()
    {
        var message = Write("Hello");

        Assert.False(message.CanBeChangedBy(Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => message.Edit(Guid.NewGuid(), "Mine now", Now));
        Assert.Throws<DomainException>(() => message.Delete(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Delete_DropsTheWords_KeepsThePlace_AndIsFinal()
    {
        var message = Write("Oops, wrong client.");

        message.Delete(Author, Now.AddMinutes(1));

        Assert.True(message.IsDeleted);
        Assert.Equal(string.Empty, message.Body);
        Assert.False(message.CanBeChangedBy(Author, Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => message.Edit(Author, "Back", Now.AddMinutes(2)));
    }

    [Fact]
    public void AReadMark_NeverMovesBack()
    {
        var read = ConversationRead.Start(Guid.NewGuid(), Guid.NewGuid(), Now);

        read.ReadUpTo(Now.AddMinutes(5));
        read.ReadUpTo(Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(5), read.LastReadAtUtc);
    }

    private static Message Write(string body) => Message.Write(Guid.NewGuid(), Guid.NewGuid(), Author, body, Now);
}
