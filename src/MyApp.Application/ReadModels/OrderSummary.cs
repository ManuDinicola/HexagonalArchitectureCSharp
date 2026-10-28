namespace MyApp.Application.ReadModels;

/// <summary>A flat row for lists. Read ports return these straight from a projection, without loading aggregates. Never an entity.</summary>
public sealed record OrderSummary(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, int LineCount);
