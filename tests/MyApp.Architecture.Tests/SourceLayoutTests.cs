using System.Text.RegularExpressions;

namespace MyApp.Architecture.Tests;

public partial class SourceLayoutTests
{
    // Top-level declarations start at column 0 (file-scoped namespaces). Nested types are indented, so they are ignored.
    [GeneratedRegex(@"^(?:(?:public|internal|file)\s+)?(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record|struct|interface|enum|union|delegate)\b", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TopLevelType();

    [Fact]
    public void Each_source_file_declares_at_most_one_type()
    {
        var violations = Repo.SourceFiles("*.cs")
            .Select(f => (File: Path.GetRelativePath(Repo.Src, f), Count: TopLevelType().Count(File.ReadAllText(f))))
            .Where(x => x.Count > 1)
            .Select(x => $"{x.File} ({x.Count} types)")
            .ToList();

        violations.ShouldBeEmpty("One class/record/interface/enum per file. Split: " + string.Join(", ", violations));
    }

    [Fact]
    public void Each_file_is_named_after_the_type_it_declares()
    {
        var violations = Repo.SourceFiles("*.cs")
            .Select(f => (File: f, Match: TypeName().Match(File.ReadAllText(f))))
            .Where(x => x.Match.Success && !NamesFile(x.Match.Groups["name"].Value, Path.GetFileNameWithoutExtension(x.File)))
            .Select(x => $"{Path.GetRelativePath(Repo.Src, x.File)} declares {x.Match.Groups["name"].Value}")
            .ToList();

        violations.ShouldBeEmpty("Name the file after its type: " + string.Join(", ", violations));
    }

    // Result<T> may live in ResultOfT.cs: a generic type may carry an "Of..." suffix.
    private static bool NamesFile(string type, string file) =>
        string.Equals(type, file, StringComparison.Ordinal)
        || (file.StartsWith(type + "Of", StringComparison.Ordinal) && file.Length > type.Length + 2);

    [GeneratedRegex(@"^(?:(?:public|internal|file)\s+)?(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+struct)?|struct|interface|enum|union)\s+(?<name>\w+)", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TypeName();
}
