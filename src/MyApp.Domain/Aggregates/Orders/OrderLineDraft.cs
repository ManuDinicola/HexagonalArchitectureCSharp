namespace MyApp.Domain.Aggregates.Orders;

/// <summary>Raw input for one line of <see cref="Order.Place"/>. Only the aggregate turns drafts into <see cref="OrderLine"/>s, so lines cannot exist outside an order.</summary>
public readonly record struct OrderLineDraft(string Sku, int Quantity, decimal UnitPrice);
