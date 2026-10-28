namespace MyApp.Api.Contracts;

public sealed record PlaceOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);
