namespace Partnr.Integrations.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<IntegrationsFactory>
{
    public const string Name = "Api";
}
