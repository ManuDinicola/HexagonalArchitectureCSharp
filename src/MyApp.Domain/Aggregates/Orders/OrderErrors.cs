using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public static class OrderErrors
{
    public static Error CustomerRequired { get; } =
        Error.Validation("Order.CustomerRequired", "An order needs a customer.", "customerId");

    public static Error NoLines { get; } =
        Error.Validation("Order.NoLines", "An order needs at least one line.", "lines");

    public static Error CustomerTooLong { get; } =
        Error.Validation("Order.CustomerTooLong", $"CustomerId cannot exceed {Order.CustomerIdMaxLength} characters.", "customerId");

    // The line errors carry the target of the line field; Order.Place prefixes it with "lines[i]." for the caller.
    public static Error SkuTooLong { get; } =
        Error.Validation("OrderLine.SkuTooLong", $"Sku cannot exceed {OrderLine.SkuMaxLength} characters.", "sku");

    public static Error UnitPriceTooPrecise { get; } =
        Error.Validation("OrderLine.UnitPriceTooPrecise", $"Unit price cannot have more than {OrderLine.UnitPriceScale} decimal places.", "unitPrice");

    public static Error SkuRequired { get; } =
        Error.Validation("OrderLine.SkuRequired", "An order line needs a SKU.", "sku");

    public static Error InvalidQuantity { get; } =
        Error.Validation("OrderLine.InvalidQuantity", "Quantity must be greater than zero.", "quantity");

    public static Error InvalidUnitPrice { get; } =
        Error.Validation("OrderLine.InvalidUnitPrice", "Unit price cannot be negative.", "unitPrice");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Order.NotFound", $"Order '{id}' was not found.");
}
