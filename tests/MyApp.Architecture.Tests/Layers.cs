using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using MyApp.Application.Messaging;
using ArchUnitArchitecture = ArchUnitNET.Domain.Architecture;

namespace MyApp.Architecture.Tests;

/// <summary>Shared handles on the four layers. Rename MyApp here if you rename the solution.</summary>
internal static class Layers
{
    public const string Domain = "MyApp.Domain";
    public const string Application = "MyApp.Application";
    public const string Infrastructure = "MyApp.Infrastructure";
    public const string Api = "MyApp.Api";

    public static readonly Assembly DomainAssembly = typeof(Domain.Common.Result).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(IDispatcher).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly ApiAssembly = Assembly.Load(Api); // its types are internal

    /// <summary>The four layers loaded once for every ArchUnitNET rule.</summary>
    public static readonly ArchUnitArchitecture Arch = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
        .Build();

    public const string DomainCommon = "MyApp.Domain.Common";
    public const string InfrastructurePersistence = "MyApp.Infrastructure.Persistence";
    public const string PortsOut = "MyApp.Application.Ports.Out";
    public const string UseCases = "MyApp.Application.UseCases";
    public const string Messaging = "MyApp.Application.Messaging";
    public const string Behaviors = "MyApp.Application.Behaviors";

    /// <summary>Regex for ArchUnitNET: these namespaces and everything below them (a plain name would match one namespace exactly).</summary>
    public static string NamespaceOrBelow(params string[] namespaces) =>
        "^(" + string.Join('|', namespaces.Select(Regex.Escape)) + @")(\..*)?$";

    /// <summary>Every Domain namespace except Common (Result/Error): aggregates, entities, value objects.</summary>
    public static string[] DomainEntityNamespaces() =>
    [
        .. DomainAssembly.GetTypes()
            .Select(t => t.Namespace)
            .OfType<string>()
            .Where(ns => !string.Equals(ns, DomainCommon, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal),
    ];
}
