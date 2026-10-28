namespace MyApp.Application.UseCases.GetOrder;

public sealed record OrderLineDto(string Sku, int Quantity, decimal UnitPrice);
