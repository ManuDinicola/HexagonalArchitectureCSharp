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

    public static Result<OrderLine> Create(string sku, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return OrderErrors.SkuRequired;
        }

        if (sku.Trim().Length > SkuMaxLength)
        {
            return OrderErrors.SkuTooLong;
        }

        if (quantity <= 0)
        {
            return OrderErrors.InvalidQuantity;
        }

        if (unitPrice < 0)
        {
            return OrderErrors.InvalidUnitPrice;
        }

        if (decimal.Round(unitPrice, UnitPriceScale) != unitPrice)
        {
            return OrderErrors.UnitPriceTooPrecise;
        }

        return new OrderLine(sku.Trim(), quantity, unitPrice);
    }
}
