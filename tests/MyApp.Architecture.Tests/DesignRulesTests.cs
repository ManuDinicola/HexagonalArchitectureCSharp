using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Messaging;
using NetArchTest.Rules;

namespace MyApp.Architecture.Tests;

public class DesignRulesTests
{
    private static void AssertNoViolations(TestResult result) =>
        result.IsSuccessful.ShouldBeTrue("Violating types: " + string.Join(", ", result.FailingTypeNames ?? []));

    [Fact]
    public void Everything_in_Infrastructure_is_internal_except_AddInfrastructure()
    {
        var publicTypes = Layers.InfrastructureAssembly.GetExportedTypes().Select(t => t.FullName);

        publicTypes.ShouldBe(["MyApp.Infrastructure.DependencyInjection"]);
    }

    [Fact]
    public void Api_exposes_nothing()
    {
        Layers.ApiAssembly.GetExportedTypes().ShouldBeEmpty("The Api is a composition root: endpoints and contracts are internal.");
    }

    [Fact]
    public void Pipeline_internals_are_not_public()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApplicationAssembly)
            .That().ResideInNamespace(Layers.Behaviors).Or().HaveName("Dispatcher")
            .Should().NotBePublic().GetResult());
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
    public void Port_methods_that_return_a_Task_take_a_CancellationToken()
    {
        var ports = Layers.ApplicationAssembly.GetTypes()
            .Where(t => t.IsInterface && string.Equals(t.Namespace, Layers.PortsOut, StringComparison.Ordinal));

        foreach (var method in ports.SelectMany(p => p.GetMethods()).Where(m => typeof(Task).IsAssignableFrom(m.ReturnType)))
        {
            method.GetParameters().LastOrDefault()?.ParameterType.ShouldBe(typeof(CancellationToken),
                $"{method.DeclaringType!.Name}.{method.Name} must end with a CancellationToken.");
        }
    }

    [Fact]
    public void Commands_implement_ICommand_and_queries_do_not()
    {
        var requests = Layers.ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && ImplementsOpen(t, typeof(IRequest<>)))
            .ToList();

        requests.ShouldNotBeEmpty();
        foreach (var request in requests)
        {
            var isCommand = ImplementsOpen(request, typeof(ICommand<>));
            if (request.Name.EndsWith("Command", StringComparison.Ordinal))
            {
                isCommand.ShouldBeTrue($"{request.Name} changes state: implement ICommand<...> so the unit of work saves it.");
            }
            else if (request.Name.EndsWith("Query", StringComparison.Ordinal))
            {
                isCommand.ShouldBeFalse($"{request.Name} is a query: implement IRequest<...>, queries never save.");
            }
            else
            {
                throw new ShouldAssertException($"{request.Name}: name requests *Command (state change) or *Query (read).");
            }
        }
    }

    [Fact]
    public void Ports_are_one_per_aggregate_not_generic()
    {
        var generic = Layers.ApplicationAssembly.GetTypes()
            .Where(t => string.Equals(t.Namespace, Layers.PortsOut, StringComparison.Ordinal) && t.IsGenericTypeDefinition);

        generic.ShouldBeEmpty();
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
    public void Use_cases_never_commit_themselves_the_pipeline_does()
    {
        AssertNoViolations(Types.InAssembly(Layers.ApplicationAssembly)
            .That().ResideInNamespaceStartingWith(Layers.UseCases)
            .ShouldNot().HaveDependencyOn(typeof(MyApp.Application.Ports.Out.IUnitOfWork).FullName!).GetResult());
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
        {
            handled.Count(h => h == request).ShouldBe(1, $"{request.Name} must have exactly one IRequestHandler.");
        }
    }

    [Fact]
    public void Entities_have_no_public_constructors_and_no_public_setters()
    {
        foreach (var type in Layers.DomainAssembly.GetTypes().Where(IsEntity))
        {
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty(
                $"{type.Name}: use a static factory method that enforces invariants, not a public constructor.");

            var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true });
            settable.ShouldBeEmpty($"{type.Name}: has public setters. Use private setters.");
        }
    }

    private static bool IsEntity(Type t) =>
        t is { IsClass: true } && t.Namespace is not null && !string.Equals(t.Namespace, Layers.DomainCommon
, StringComparison.Ordinal) && !(t.IsAbstract && t.IsSealed) // static classes such as OrderErrors
        && !t.Name.StartsWith('<');

    private static bool ImplementsOpen(Type type, Type openInterface) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);
}
