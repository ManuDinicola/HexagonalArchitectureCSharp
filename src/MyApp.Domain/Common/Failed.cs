namespace MyApp.Domain.Common;

/// <summary>The failure case of every result: one or more expected errors.</summary>
public sealed record Failed(IReadOnlyList<Error> Errors);
