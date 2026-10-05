namespace ClientPortal.IntegrationTests;

/// <summary>Shares one <see cref="ApiFactory"/> (and so one test database) across all test classes.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
