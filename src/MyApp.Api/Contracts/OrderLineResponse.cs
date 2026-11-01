namespace MyApp.Api.Contracts;

internal sealed record OrderLineResponse(string Sku, int Quantity, decimal UnitPrice);
