using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public sealed class OrderLine
{
    public const int SkuMaxLength = 50;
    public const int UnitPriceScale = 2;

    private OrderLine() { } // EF Core

    private OrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Total => Quantity * UnitPrice;

    /// <summary>Internal on purpose: a line only exists inside an order, created by <see cref="Order.Place"/>. Reports every broken rule, not just the first.</summary>
    internal static Result<OrderLine> Create(string sku, int quantity, decimal unitPrice)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(sku))
        {
            errors.Add(OrderErrors.SkuRequired);
        }
        else if (sku.Trim().Length > SkuMaxLength)
        {
            errors.Add(OrderErrors.SkuTooLong);
        }

        if (quantity <= 0)
        {
            errors.Add(OrderErrors.InvalidQuantity);
        }

        if (unitPrice < 0)
        {
            errors.Add(OrderErrors.InvalidUnitPrice);
        }
        else if (decimal.Round(unitPrice, UnitPriceScale) != unitPrice)
        {
            errors.Add(OrderErrors.UnitPriceTooPrecise);
        }

        if (errors.Count > 0)
        {
            return Result.Failure<OrderLine>(errors);
        }

        return new OrderLine(sku.Trim(), quantity, unitPrice);
    }
}
