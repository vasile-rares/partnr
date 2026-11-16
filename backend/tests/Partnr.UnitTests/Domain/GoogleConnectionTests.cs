using Partnr.Domain.Entities;
using Partnr.Domain.Enums;
using Partnr.Domain.Exceptions;

namespace Partnr.UnitTests.Domain;

public class GoogleConnectionTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Connect_StartsConnected_WithTheTrimmedEmail()
    {
        var connection = GoogleConnection.Connect(Guid.NewGuid(), "  ana@gmail.example ", "sealed", Now);

        Assert.Equal(GoogleConnectionStatus.Connected, connection.Status);
        Assert.Equal("ana@gmail.example", connection.GoogleEmail);
        Assert.Equal(Now, connection.ConnectedAtUtc);
    }

    [Theory]
    [InlineData("", "sealed")]
    [InlineData("  ", "sealed")]
    [InlineData("ana@gmail.example", "")]
    public void Connect_WithoutAnEmailOrAToken_Throws(string email, string token) =>
        Assert.Throws<DomainException>(() => GoogleConnection.Connect(Guid.NewGuid(), email, token, Now));

    [Fact]
    public void Connect_WithoutAnAccount_Throws() =>
        Assert.Throws<DomainException>(() => GoogleConnection.Connect(Guid.Empty, "ana@gmail.example", "sealed", Now));

    [Fact]
    public void Reconnect_ReplacesTheTokenAndTheAccount_AndClearsTheWarning()
    {
        var connection = GoogleConnection.Connect(Guid.NewGuid(), "ana@gmail.example", "old", Now);
        connection.MarkNeedsReconnect();
        Assert.Equal(GoogleConnectionStatus.NeedsReconnect, connection.Status);

        connection.Reconnect("work@gmail.example", "new", Now.AddDays(1));

        Assert.Equal(GoogleConnectionStatus.Connected, connection.Status);
        Assert.Equal("work@gmail.example", connection.GoogleEmail);
        Assert.Equal("new", connection.ProtectedRefreshToken);
        Assert.Equal(Now.AddDays(1), connection.ConnectedAtUtc);
    }
}
