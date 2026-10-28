namespace MyApp.Api.Contracts;

public sealed record PlaceOrderRequest(string CustomerId, List<PlaceOrderLineRequest> Lines);

public sealed record PlaceOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderResponse(Guid OrderId);

public sealed record OrderResponse(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, List<OrderLineResponse> Lines);

public sealed record OrderLineResponse(string Sku, int Quantity, decimal UnitPrice);
