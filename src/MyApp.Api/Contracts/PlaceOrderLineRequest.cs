namespace MyApp.Api.Contracts;

internal sealed record PlaceOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);
