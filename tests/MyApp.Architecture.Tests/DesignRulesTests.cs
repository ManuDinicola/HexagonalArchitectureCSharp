using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Messaging;
using NetArchTest.Rules;

namespace MyApp.Architecture.Tests;

public class DesignRulesTests
{
    private static void AssertNoViolations(TestResult result) =>
        Assert.True(result.IsSuccessful, "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []));

    [Fact]
    public void Everything_in_Infrastructure_is_internal_except_AddInfrastructure()
    {
        var publicTypes = Layers.InfrastructureAssembly.GetExportedTypes().Select(t => t.FullName);

        Assert.Equal(["MyApp.Infrastructure.DependencyInjection"], publicTypes);
    }

    [Fact]
    public void Persistence_types_are_not_public()
    {
        AssertNoViolations(Types.InAssembly(Layers.InfrastructureAssembly)
            .That().ResideInNamespace("MyApp.Infrastructure.Persistence")
            .Should().NotBePublic().GetResult());
        AssertNoViolations(Types.InAssembly(Layers.InfrastructureAssembly)
            .That().Inherit(typeof(DbContext)).Or().ImplementInterface(typeof(IEntityTypeConfiguration<>))
            .Should().NotBePublic().GetResult());
    }

    [Fact]
    public void Ports_are_interfaces_that_only_know_the_domain()
    {
        var ports = Types.InAssembly(Layers.ApplicationAssembly).That().ResideInNamespace(Layers.PortsOut);

        AssertNoViolations(ports.Should().BeInterfaces().GetResult());
        AssertNoViolations(ports.ShouldNot()
            .HaveDependencyOnAny(Layers.UseCases, Layers.Messaging, Layers.Behaviors,
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions").GetResult());
    }

    [Fact]
    public void Ports_are_one_per_aggregate_not_generic()
    {
        var generic = Layers.ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace == Layers.PortsOut && t.IsGenericTypeDefinition);

        Assert.Empty(generic);
    }

    [Fact]
    public void Handlers_and_validators_are_internal_and_live_in_UseCases()
    {
        var types = Types.InAssembly(Layers.ApplicationAssembly)
            .That().ImplementInterface(typeof(IRequestHandler<,>)).Or().ImplementInterface(typeof(IValidator<>));

        AssertNoViolations(types.Should().NotBePublic().GetResult());
        AssertNoViolations(types.Should().ResideInNamespaceStartingWith(Layers.UseCases).GetResult());
    }

    [Fact]
    public void Every_request_has_exactly_one_handler()
    {
        var assembly = Layers.ApplicationAssembly;
        var requests = assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && ImplementsOpen(t, typeof(IRequest<>)));
        var handled = assembly.GetTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .Select(i => i.GetGenericArguments()[0])
            .ToList();

        foreach (var request in requests)
            Assert.True(handled.Count(h => h == request) == 1, $"{request.Name} must have exactly one IRequestHandler.");
    }

    [Fact]
    public void Entities_have_no_public_constructors_and_no_public_setters()
    {
        foreach (var type in Layers.DomainAssembly.GetTypes().Where(IsEntity))
        {
            Assert.True(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0,
                $"{type.Name}: use a static factory method that enforces invariants, not a public constructor.");

            var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true });
            Assert.True(!settable.Any(), $"{type.Name}: public setters on {string.Join(", ", settable.Select(p => p.Name))}. Use private setters.");
        }
    }

    private static bool IsEntity(Type t) =>
        t is { IsClass: true } && t.Namespace is not null && t.Namespace != Layers.DomainCommon
        && !(t.IsAbstract && t.IsSealed) // static classes such as OrderErrors
        && !t.Name.StartsWith('<');

    private static bool ImplementsOpen(Type type, Type openInterface) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);
}
