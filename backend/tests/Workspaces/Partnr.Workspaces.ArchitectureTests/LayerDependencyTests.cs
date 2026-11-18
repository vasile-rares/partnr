using System.Reflection;
using Partnr.Workspaces.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace Partnr.Workspaces.ArchitectureTests;

/// <summary>The dependency rules from backend/CLAUDE.md.</summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(Partnr.Workspaces.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Partnr.Workspaces.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_else_in_the_solution_or_on_ef_core()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Partnr.Workspaces.Application",
                "Partnr.Workspaces.Infrastructure",
                "Partnr.Workspaces.Api",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_ef_core_or_asp_net_core()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Partnr.Workspaces.Infrastructure",
                "Partnr.Workspaces.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Partnr.Workspaces.Api")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Api_does_not_use_persistence_types()
    {
        var result = Types.InAssembly(Api)
            .ShouldNot()
            .HaveDependencyOn("Partnr.Workspaces.Infrastructure.Persistence")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_services_are_not_public()
    {
        var result = Types.InAssembly(Application)
            .That()
            .ResideInNamespace("Partnr.Workspaces.Application.Services")
            .And()
            .AreClasses()
            .ShouldNot()
            .BePublic()
            .GetResult();

        AssertSuccessful(result);
    }

    // Helpers, mappers and access rules have their own folders (Mappings, Authorization, Common); this one is for use cases.
    [Fact]
    public void Application_services_folder_holds_only_services()
    {
        var result = Types.InAssembly(Application)
            .That()
            .ResideInNamespace("Partnr.Workspaces.Application.Services")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Service")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Controllers_are_named_with_the_controller_suffix()
    {
        var result = Types.InAssembly(Api)
            .That()
            .Inherit(typeof(ControllerBase))
            .Should()
            .HaveNameEndingWith("Controller")
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(TestResult result) =>
        Assert.True(
            result.IsSuccessful,
            "Types breaking the rule: " + string.Join(", ", result.FailingTypeNames ?? []));
}
