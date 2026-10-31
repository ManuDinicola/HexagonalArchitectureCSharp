namespace MyApp.Benchmarks.HandwrittenUnion;

/// <summary>The failure case of every result: one or more expected errors.</summary>
public sealed record Failed(IReadOnlyList<Error> Errors);
