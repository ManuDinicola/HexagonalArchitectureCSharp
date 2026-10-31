namespace MyApp.Benchmarks;

/// <summary>The same two errors for both versions, built once so creating a failure does not measure building errors.</summary>
internal static class Errors
{
    public static IReadOnlyList<MyApp.Domain.Common.Error> Candidate { get; } =
    [
        MyApp.Domain.Common.Error.Validation("Order.Empty", "An order needs lines.", "lines"),
        MyApp.Domain.Common.Error.Conflict("Order.Duplicate", "Already placed."),
    ];

    public static IReadOnlyList<KeywordUnion.Error> Baseline { get; } =
    [
        KeywordUnion.Error.Validation("Order.Empty", "An order needs lines.", "lines"),
        KeywordUnion.Error.Conflict("Order.Duplicate", "Already placed."),
    ];
}
