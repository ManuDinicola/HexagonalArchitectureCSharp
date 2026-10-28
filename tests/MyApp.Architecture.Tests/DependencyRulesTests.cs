using NetArchTest.Rules;

namespace MyApp.Architecture.Tests;

public class DependencyRulesTests
{
    private static void AssertNoViolations(TestResult result) =>
        result.IsSuccessful.ShouldBeTrue("Violating types: " + string.Join(", ", result.FailingTypeNames ?? []));

    [Fact]
    public void Domain_depends_on_nothing_of_ours()
    {
        AssertNoViolations(Types.InAssembly(Layers.DomainAssembly).ShouldNot()
            .HaveDependencyOnAny(Layers.Application, Layers.Infrastructure, Layers.Api).GetResult());
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApplicationAssembly).ShouldNot()
            .HaveDependencyOnAny(Layers.Infrastructure, Layers.Api).GetResult());
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        AssertNoViolations(Types.InAssembly(Layers.InfrastructureAssembly).ShouldNot()
            .HaveDependencyOn(Layers.Api).GetResult());
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Data.Sqlite")]
    public void Domain_and_Application_do_not_reference_frameworks(string forbidden)
    {
        AssertNoViolations(Types.InAssembly(Layers.DomainAssembly).ShouldNot().HaveDependencyOn(forbidden).GetResult());
        AssertNoViolations(Types.InAssembly(Layers.ApplicationAssembly).ShouldNot().HaveDependencyOn(forbidden).GetResult());
    }

    [Fact]
    public void Project_references_follow_the_hexagon()
    {
        static string[] Refs(System.Reflection.Assembly a)
        {
            return [.. a.GetReferencedAssemblies().Select(r => r.Name!).Where(n => n.StartsWith("MyApp.", StringComparison.Ordinal))];
        }

        Refs(Layers.DomainAssembly).ShouldBeEmpty();
        Refs(Layers.ApplicationAssembly).ShouldBe([Layers.Domain]);
        Refs(Layers.InfrastructureAssembly).ShouldContain(Layers.Application, StringComparer.Ordinal);
        Refs(Layers.InfrastructureAssembly).ShouldBeSubsetOf([Layers.Application, Layers.Domain]);
        Refs(Layers.ApiAssembly).ShouldBeSubsetOf([Layers.Application, Layers.Domain, Layers.Infrastructure]);
    }

    [Fact]
    public void Api_uses_Infrastructure_only_through_AddInfrastructure()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApiAssembly).ShouldNot()
            .HaveDependencyOn(Layers.InfrastructurePersistence).GetResult());
    }

    [Fact]
    public void Api_does_not_use_outbound_ports()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApiAssembly).ShouldNot().HaveDependencyOn(Layers.PortsOut).GetResult());
    }

    [Fact]
    public void Api_does_not_use_domain_entities()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApiAssembly).ShouldNot()
            .HaveDependencyOnAny(Layers.DomainEntityNamespaces()).GetResult());
    }

    [Fact]
    public void Infrastructure_does_not_use_UseCases_Messaging_or_Behaviors()
    {
        AssertNoViolations(Types.InAssembly(Layers.InfrastructureAssembly).ShouldNot()
            .HaveDependencyOnAny(Layers.UseCases, Layers.Messaging, Layers.Behaviors).GetResult());
    }
}
