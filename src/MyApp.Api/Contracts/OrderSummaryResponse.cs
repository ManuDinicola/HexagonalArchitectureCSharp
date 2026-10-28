namespace MyApp.Api.Contracts;

internal sealed record OrderSummaryResponse(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, int LineCount);
