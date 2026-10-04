using System.Reflection;
using ClientPortal.Api.Controllers;
using ClientPortal.Domain.Entities;
using ClientPortal.SharedKernel.Primitives;
using NetArchTest.Rules;
using ApplicationAssemblyMarker = ClientPortal.Application.DependencyInjection;
using InfrastructureAssemblyMarker = ClientPortal.Infrastructure.DependencyInjection;

namespace ClientPortal.Architecture.Tests;

public class DependencyRulesTests
{
    private const string SharedKernel = "ClientPortal.SharedKernel";
    private const string Domain = "ClientPortal.Domain";
    private const string Application = "ClientPortal.Application";
    private const string Infrastructure = "ClientPortal.Infrastructure";
    private const string Api = "ClientPortal.Api";

    private static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
    private static readonly Assembly DomainAssembly = typeof(Client).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ApplicationAssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureAssemblyMarker).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ClientsController).Assembly;

    [Fact]
    public void SharedKernel_DoesNotDependOnOtherLayers() =>
        AssertNoDependencies(SharedKernelAssembly, Domain, Application, Infrastructure, Api);

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers() =>
        AssertNoDependencies(DomainAssembly, Application, Infrastructure, Api);

    [Fact]
    public void Application_DoesNotDependOnOuterLayers() =>
        AssertNoDependencies(ApplicationAssembly, Infrastructure, Api);

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        AssertNoDependencies(InfrastructureAssembly, Api);

    [Fact]
    public void Api_DoesNotDependOnInfrastructurePersistence() =>
        AssertNoDependencies(ApiAssembly, $"{Infrastructure}.Persistence");

    private static void AssertNoDependencies(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
