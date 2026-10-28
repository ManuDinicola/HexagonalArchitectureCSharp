using System.Diagnostics;

namespace MyApp.Architecture.Tests;

/// <summary>
/// Guards the guardrails: the build rules themselves cannot be weakened or silently disappear without a test failing.
/// Editing this file or the files it checks is a human decision (see CODEOWNERS).
/// </summary>
public class GuardrailIntegrityTests
{
    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Repo.Root, relativePath));

    [Fact]
    public void Banned_api_violations_and_warnings_still_break_the_build()
    {
        Read(".editorconfig").ShouldContain("dotnet_diagnostic.RS0030.severity = error");

        var props = Read("Directory.Build.props");
        props.ShouldContain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        props.ShouldContain("<AnalysisMode>All</AnalysisMode>");
        props.ShouldContain("<Nullable>enable</Nullable>");
    }

    [Fact]
    public void Every_production_project_gets_the_banned_api_analyzer_and_the_common_list()
    {
        var props = Read("src/Directory.Build.props");

        props.ShouldContain("Microsoft.CodeAnalysis.BannedApiAnalyzers");
        props.ShouldContain("BannedSymbols.Common.txt");
        props.ShouldContain("BannedSymbols.txt");
    }

    [Theory]
    [InlineData("P:System.DateTime.UtcNow")]
    [InlineData("P:System.DateTimeOffset.UtcNow")]
    [InlineData("M:System.Guid.NewGuid")]
    [InlineData("P:System.Threading.Tasks.Task`1.Result")]
    [InlineData("M:System.Threading.Tasks.Task.Wait();")]
    [InlineData("M:System.Runtime.CompilerServices.TaskAwaiter.GetResult")]
    [InlineData("M:System.Threading.Thread.Sleep(System.Int32)")]
    public void Common_banned_list_keeps_its_core_entries(string entry)
    {
        Read("src/BannedSymbols.Common.txt").ShouldContain(entry);
    }

    [Fact]
    public void Layer_specific_banned_lists_still_exist()
    {
        foreach (var project in new[] { Layers.Domain, Layers.Application, Layers.Infrastructure, Layers.Api })
        {
            File.Exists(Path.Combine(Repo.Src, project, "BannedSymbols.txt")).ShouldBeTrue($"{project}/BannedSymbols.txt was removed.");
        }
    }

    [Fact]
    public void Every_namespace_named_in_a_banned_list_exists()
    {
        // A typo or a renamed folder would turn a rule into a silent no-op.
        string[] known =
        [
            .. new[] { Layers.DomainAssembly, Layers.ApplicationAssembly, Layers.InfrastructureAssembly, Layers.ApiAssembly }
                .SelectMany(a => a.GetTypes())
                .Select(t => t.Namespace)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal),
        ];

        var missing = Directory.EnumerateFiles(Repo.Src, "BannedSymbols*.txt", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines)
            .Where(l => l.StartsWith("N:MyApp.", StringComparison.Ordinal))
            .Select(l => l[2..l.IndexOf(';', StringComparison.Ordinal)])
            .Where(ns => !known.Any(k => k.StartsWith(ns, StringComparison.Ordinal)))
            .ToList();

        missing.ShouldBeEmpty("Banned namespaces without any type: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_source_file_is_hidden_by_gitignore()
    {
        // A pattern such as "out/" once hid Application/Ports/Out: the build passed locally and failed on every clone.
        if (!Directory.Exists(Path.Combine(Repo.Root, ".git")))
        {
            return;
        }

        var files = new[] { "src", "tests" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(Repo.Root, d), "*", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Repo.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

        var info = new ProcessStartInfo("git", "check-ignore --no-index --stdin")
        {
            WorkingDirectory = Repo.Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var git = Process.Start(info)!;
        git.StandardInput.Write(string.Join('\n', files));
        git.StandardInput.Close();
        var ignored = git.StandardOutput.ReadToEnd();
        git.WaitForExit();

        ignored.Trim().ShouldBeEmpty("These files are ignored by .gitignore and would be missing from a clone:\n" + ignored);
    }
}
