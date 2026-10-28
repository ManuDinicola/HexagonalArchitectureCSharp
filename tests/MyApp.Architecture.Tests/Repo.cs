namespace MyApp.Architecture.Tests;

/// <summary>Finds files of the repository on disk, for rules about source and project files rather than compiled types.</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Src => Path.Combine(Root, "src");

    public static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Src, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the .slnx from " + AppContext.BaseDirectory);
    }
}
