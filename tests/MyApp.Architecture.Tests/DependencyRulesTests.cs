using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace MyApp.Architecture.Tests;

public class DependencyRulesTests
{
    /// <summary>Nothing in <paramref name="from"/> may depend on these namespaces (or the ones below them).</summary>
    private static void MustNotDependOn(Assembly from, params string[] namespaces) =>
        Types().That().ResideInAssembly(from).Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(Layers.NamespaceOrBelow(namespaces))
            .Check(Layers.Arch);

    [Fact]
    public void Domain_depends_on_nothing_of_ours()
    {
        MustNotDependOn(Layers.DomainAssembly, Layers.Application, Layers.Infrastructure, Layers.Api);
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        MustNotDependOn(Layers.ApplicationAssembly, Layers.Infrastructure, Layers.Api);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        MustNotDependOn(Layers.InfrastructureAssembly, Layers.Api);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Data.Sqlite")]
    public void Domain_and_Application_do_not_reference_frameworks(string forbidden)
    {
        MustNotDependOn(Layers.DomainAssembly, forbidden);
        MustNotDependOn(Layers.ApplicationAssembly, forbidden);
    }

    [Fact]
    public void Project_references_follow_the_hexagon()
    {
        static string[] Refs(Assembly a)
        {
            return [.. a.GetReferencedAssemblies().Select(r => r.Name!).Where(n => n.StartsWith("MyApp.", StringComparison.Ordinal))];
        }

        Refs(Layers.DomainAssembly).ShouldBeEmpty();
        Refs(Layers.ApplicationAssembly).ShouldBe([Layers.Domain]);
        Refs(Layers.InfrastructureAssembly).Except([Layers.Application, Layers.Domain], StringComparer.Ordinal).ShouldBeEmpty();
        Refs(Layers.ApiAssembly).Except([Layers.Application, Layers.Domain, Layers.Infrastructure], StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    public void Api_uses_Infrastructure_only_through_AddInfrastructure()
    {
        MustNotDependOn(Layers.ApiAssembly, Layers.InfrastructurePersistence);
    }

    [Fact]
    public void Api_does_not_use_outbound_ports()
    {
        MustNotDependOn(Layers.ApiAssembly, Layers.PortsOut);
    }

    [Fact]
    public void Api_does_not_use_domain_entities()
    {
        MustNotDependOn(Layers.ApiAssembly, Layers.DomainEntityNamespaces());
    }

    [Fact]
    public void Infrastructure_does_not_use_UseCases_Messaging_or_Behaviors()
    {
        MustNotDependOn(Layers.InfrastructureAssembly, Layers.UseCases, Layers.Messaging, Layers.Behaviors);
    }
}
