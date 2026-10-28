using System.Text.RegularExpressions;

namespace MyApp.Architecture.Tests;

public partial class SourceLayoutTests
{
    // Top-level declarations start at column 0 (file-scoped namespaces). Nested types are indented, so they are ignored.
    [GeneratedRegex(@"^(?:(?:public|internal|file)\s+)?(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record|struct|interface|enum|union|delegate)\b", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    [Fact]
    public void Each_source_file_declares_at_most_one_type()
    {
        var src = Path.Combine(FindRepoRoot(), "src");
        var violations = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (File: Path.GetRelativePath(src, f), Count: TopLevelType().Count(File.ReadAllText(f))))
            .Where(x => x.Count > 1)
            .Select(x => $"{x.File} ({x.Count} types)")
            .ToList();

        violations.ShouldBeEmpty("One class/record/interface/enum per file. Split: " + string.Join(", ", violations));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the .slnx from " + AppContext.BaseDirectory);
    }
}
