namespace MyApp.Benchmarks;

/// <summary>The same two errors for both versions, built once so creating a failure does not measure building errors.</summary>
internal static class Errors
{
    public static IReadOnlyList<HandwrittenUnion.Error> Candidate { get; } =
    [
        HandwrittenUnion.Error.Validation("Order.Empty", "An order needs lines.", "lines"),
        HandwrittenUnion.Error.Conflict("Order.Duplicate", "Already placed."),
    ];

    public static IReadOnlyList<KeywordUnion.Error> Baseline { get; } =
    [
        KeywordUnion.Error.Validation("Order.Empty", "An order needs lines.", "lines"),
        KeywordUnion.Error.Conflict("Order.Duplicate", "Already placed."),
    ];
}
