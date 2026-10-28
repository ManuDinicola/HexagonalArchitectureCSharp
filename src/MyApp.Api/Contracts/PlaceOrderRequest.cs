namespace MyApp.Api.Contracts;

internal sealed record PlaceOrderRequest(string CustomerId, List<PlaceOrderLineRequest> Lines);
