namespace MyApp.Api.Contracts;

public sealed record PlaceOrderRequest(string CustomerId, List<PlaceOrderLineRequest> Lines);
