namespace MyApp.Application.UseCases.PlaceOrder;

public sealed record PlaceOrderLine(string Sku, int Quantity, decimal UnitPrice);
