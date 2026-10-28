using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyApp.Architecture.Tests;

/// <summary>Source-file rules, checked on the real syntax tree (Roslyn), not on text.</summary>
public class SourceLayoutTests
{
    // Preview: the Domain uses C# 15 union types.
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static SyntaxTree Parse(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseOptions, file);

    /// <summary>Types declared directly in the file or in its namespace. Nested types are not top-level.</summary>
    private static List<string> TopLevelTypeNames(SyntaxTree tree) =>
    [
        .. tree.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .Select(n => n switch
            {
                BaseTypeDeclarationSyntax t => t.Identifier.ValueText,
                DelegateDeclarationSyntax d => d.Identifier.ValueText,
                _ => null,
            })
            .OfType<string>(),
    ];

    [Fact]
    public void Every_source_file_parses()
    {
        // If the parser cannot read a file, the two rules below would silently see nothing.
        var violations = Repo.SourceFiles("*.cs")
            .Select(f => (File: f, Error: Parse(f).GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error)))
            .Where(x => x.Error is not null)
            .Select(x => $"{Path.GetRelativePath(Repo.Src, x.File)}: {x.Error}")
            .ToList();

        violations.ShouldBeEmpty("Syntax errors: " + string.Join("; ", violations));
    }

    [Fact]
    public void The_parser_sees_the_union_types_of_the_Domain()
    {
        // Guards the guard: a Roslyn that does not know unions would drop Result<T> and the rules would pass for the wrong reason.
        var file = Repo.SourceFiles("ResultOfT.cs").ShouldHaveSingleItem();

        TopLevelTypeNames(Parse(file)).ShouldBe(["Result"]);
    }

    [Fact]
    public void Each_source_file_declares_at_most_one_type()
    {
        var violations = Repo.SourceFiles("*.cs")
            .Select(f => (File: f, Types: TopLevelTypeNames(Parse(f))))
            .Where(x => x.Types.Count > 1)
            .Select(x => $"{Path.GetRelativePath(Repo.Src, x.File)} ({string.Join(", ", x.Types)})")
            .ToList();

        violations.ShouldBeEmpty("One class/record/interface/enum per file. Split: " + string.Join("; ", violations));
    }

    [Fact]
    public void Each_file_is_named_after_the_type_it_declares()
    {
        var violations = Repo.SourceFiles("*.cs")
            .Select(f => (File: f, Types: TopLevelTypeNames(Parse(f))))
            .Where(x => x.Types.Count == 1 && !NamesFile(x.Types[0], Path.GetFileNameWithoutExtension(x.File)))
            .Select(x => $"{Path.GetRelativePath(Repo.Src, x.File)} declares {x.Types[0]}")
            .ToList();

        violations.ShouldBeEmpty("Name the file after its type: " + string.Join("; ", violations));
    }

    [Fact]
    public void Production_code_never_suppresses_a_warning()
    {
        string[] forbidden = ["#pragma warning disable", "SuppressMessage", "[assembly: SuppressMessage", "<NoWarn>"];
        var violations = Repo.SourceFiles("*.cs").Concat(Repo.SourceFiles("*.csproj"))
            .Where(f => forbidden.Any(token => File.ReadAllText(f).Contains(token, StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(Repo.Src, f))
            .ToList();

        violations.ShouldBeEmpty("Fix the code instead of silencing the rule: " + string.Join(", ", violations));
    }

    // Result<T> may live in ResultOfT.cs: a generic type may carry an "Of..." suffix.
    private static bool NamesFile(string type, string file) =>
        string.Equals(type, file, StringComparison.Ordinal)
        || (file.StartsWith(type + "Of", StringComparison.Ordinal) && file.Length > type.Length + 2);
}
