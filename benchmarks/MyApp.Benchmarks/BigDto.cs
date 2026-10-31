namespace MyApp.Benchmarks;

/// <summary>A large reference payload shaped like an order read model: header plus 100 lines.</summary>
public sealed record BigDto(Guid Id, string CustomerId, DateTimeOffset PlacedAt, IReadOnlyList<BigDtoLine> Lines);
