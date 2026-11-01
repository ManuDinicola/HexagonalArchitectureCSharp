using System.Xml.Linq;

namespace MyApp.Architecture.Tests;

/// <summary>
/// The assembly-reference tests only see references that are actually used. These read the .csproj files, so an unused but
/// forbidden reference (an EF package in Application, Api referencing a new project) fails the build too.
/// </summary>
public class ProjectFileTests
{
    private static readonly string[] ApplicationPackages =
    [
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
    ];

    private static (string[] Projects, string[] Packages) ReferencesOf(string project)
    {
        var doc = XDocument.Load(Path.Combine(Repo.Src, project, project + ".csproj"));
        string[] Includes(string element) =>
        [
            .. doc.Descendants(element)
                .Select(e => (string?)e.Attribute("Include"))
                .OfType<string>()
                .Select(i => string.Equals(element, "ProjectReference", StringComparison.Ordinal) ? Path.GetFileNameWithoutExtension(i.Replace('\\', '/')) : i),
        ];

        return (Includes("ProjectReference"), Includes("PackageReference"));
    }

    [Fact]
    public void Domain_references_nothing()
    {
        var (projects, packages) = ReferencesOf(Layers.Domain);

        projects.ShouldBeEmpty();
        packages.ShouldBeEmpty();
    }

    [Fact]
    public void Application_references_only_Domain_and_the_framework_abstractions()
    {
        var (projects, packages) = ReferencesOf(Layers.Application);

        projects.ShouldBe([Layers.Domain]);
        packages.Except(ApplicationPackages, StringComparer.Ordinal).ShouldBeEmpty(
            "Application may only use abstractions packages. Anything else belongs in an adapter behind a port.");
    }

    [Fact]
    public void Infrastructure_references_only_Application()
    {
        ReferencesOf(Layers.Infrastructure).Projects.ShouldBe([Layers.Application]);
    }

    [Fact]
    public void Api_references_only_Application_and_Infrastructure()
    {
        ReferencesOf(Layers.Api).Projects.ShouldBe([Layers.Application, Layers.Infrastructure], ignoreOrder: true);
    }
}
