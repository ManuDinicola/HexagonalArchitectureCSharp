namespace MyApp.Api.Contracts;

public sealed record OrderResponse(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, List<OrderLineResponse> Lines);
