using Partnr.Storage.Domain.Entities;
using Partnr.Storage.Domain.Exceptions;

namespace Partnr.Storage.UnitTests.Domain;

public class StoredFileTests
{
    private static readonly string Sha = new('a', 64);
    private static readonly DateTime Now = new(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_KeepsWhatWasGiven()
    {
        var file = StoredFile.Create("a/b/c", 10, " application/pdf ", Sha, Now);

        Assert.Equal("a/b/c", file.Key);
        Assert.Equal(10, file.SizeBytes);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(Sha, file.Sha256);
        Assert.Equal(Now, file.CreatedAtUtc);
    }

    [Fact]
    public void Create_WithoutAContentType_FallsBackToBinary()
    {
        Assert.Equal("application/octet-stream", StoredFile.Create("a", 1, "", Sha, Now).ContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAKey_Throws(string key)
    {
        Assert.Throws<DomainException>(() => StoredFile.Create(key, 1, "text/plain", Sha, Now));
    }

    [Fact]
    public void Create_WithAKeyThatIsTooLong_Throws()
    {
        Assert.Throws<DomainException>(() => StoredFile.Create(new string('k', StoredFile.MaxKeyLength + 1), 1, "text/plain", Sha, Now));
    }

    [Fact]
    public void Create_WithANegativeSize_Throws()
    {
        Assert.Throws<DomainException>(() => StoredFile.Create("a", -1, "text/plain", Sha, Now));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Create_WithAChecksumThatIsNotLowercaseSha256Hex_Throws(string sha)
    {
        Assert.Throws<DomainException>(() => StoredFile.Create("a", 1, "text/plain", sha, Now));
    }

    [Fact]
    public void Replace_ChangesTheDetails_AndKeepsTheCreationTime()
    {
        var file = StoredFile.Create("a", 1, "text/plain", Sha, Now);

        file.Replace(5, "application/zip", new string('b', 64), Now.AddHours(1));

        Assert.Equal(5, file.SizeBytes);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal(Now, file.CreatedAtUtc);
        Assert.Equal(Now.AddHours(1), file.UpdatedAtUtc);
    }
}
