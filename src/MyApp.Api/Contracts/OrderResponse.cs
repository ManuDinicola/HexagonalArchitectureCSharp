namespace MyApp.Api.Contracts;

internal sealed record OrderResponse(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, List<OrderLineResponse> Lines);
