using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public static class OrderErrors
{
    public static Error CustomerRequired { get; } =
        Error.Validation("Order.CustomerRequired", "An order needs a customer.");

    public static Error NoLines { get; } =
        Error.Validation("Order.NoLines", "An order needs at least one line.");

    public static Error SkuRequired { get; } =
        Error.Validation("OrderLine.SkuRequired", "An order line needs a SKU.");

    public static Error InvalidQuantity { get; } =
        Error.Validation("OrderLine.InvalidQuantity", "Quantity must be greater than zero.");

    public static Error InvalidUnitPrice { get; } =
        Error.Validation("OrderLine.InvalidUnitPrice", "Unit price cannot be negative.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Order.NotFound", $"Order '{id}' was not found.");
}
