using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClientPortal.Infrastructure;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Seeding;

[Collection(ApiCollection.Name)]
public sealed class ReseedTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task Reseed_LeavesUpToDateDataAlone_AndRecreatesTheDatabaseOnlyWhenTheSeedChanged()
    {
        await factory.Services.ReseedDevelopmentDataIfChangedAsync();
        var fingerprint = await StoredComment();
        Assert.Matches(new Regex("^seed:[0-9a-f]{16}$"), fingerprint);

        // Something entered by hand survives while the seed is unchanged...
        var email = $"hand-made-{Guid.NewGuid():N}@example.com";
        await Register(email);
        await factory.Services.ReseedDevelopmentDataIfChangedAsync();
        Assert.Equal(HttpStatusCode.OK, await LoginStatus(email));

        // ...and is gone once the seed has changed (simulated by an older fingerprint): the database is rebuilt.
        await SetComment("seed:0000000000000000");
        await factory.Services.ReseedDevelopmentDataIfChangedAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(email));
        Assert.Equal(HttpStatusCode.OK, await LoginStatus(DevelopmentSeed.OwnerEmail, DevelopmentSeed.Password));
        Assert.Equal(fingerprint, await StoredComment());
    }

    [Fact]
    public async Task Reseed_OfADatabaseSeededBeforeFingerprintsExisted_RecreatesIt()
    {
        await factory.Services.ReseedDevelopmentDataIfChangedAsync();
        var email = $"hand-made-{Guid.NewGuid():N}@example.com";
        await Register(email);
        await SetComment(""); // what a database seeded by the old `--seed` has: demo data, but no fingerprint

        await factory.Services.ReseedDevelopmentDataIfChangedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(email));
        Assert.Equal(HttpStatusCode.OK, await LoginStatus(DevelopmentSeed.OwnerEmail, DevelopmentSeed.Password));
    }

    private async Task Register(string email)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register",
            new { email, password = Password, fullName = "Hand Made" });
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpStatusCode> LoginStatus(string email, string password = Password) =>
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode;

    private async Task<string> StoredComment()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.Database
            .SqlQueryRaw<string>("SELECT COALESCE(shobj_description(oid, 'pg_database'), '') AS \"Value\" FROM pg_database WHERE datname = current_database()")
            .SingleAsync();
    }

    private async Task SetComment(string comment)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // DO blocks take no parameters; the comments are fixed literals of this test.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync(
            $"DO $$ BEGIN EXECUTE format('COMMENT ON DATABASE %I IS %L', current_database(), '{comment}'); END $$");
#pragma warning restore EF1002
    }
}
