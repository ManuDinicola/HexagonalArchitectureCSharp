using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public sealed class Order
{
    public const int CustomerIdMaxLength = 100;

    private readonly List<OrderLine> _lines = [];

    private Order() { } // EF Core

    private Order(Guid id, string customerId, DateTimeOffset placedAt, IEnumerable<OrderLine> lines)
    {
        Id = id;
        CustomerId = customerId;
        PlacedAt = placedAt;
        _lines.AddRange(lines);
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = null!;
    public DateTimeOffset PlacedAt { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.Total);

    /// <summary>The only way to create an order, so invariants cannot be skipped.</summary>
    public static Result<Order> Place(string customerId, IReadOnlyCollection<OrderLine> lines, DateTimeOffset placedAt)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return OrderErrors.CustomerRequired;
        }

        if (customerId.Trim().Length > CustomerIdMaxLength)
        {
            return OrderErrors.CustomerTooLong;
        }

        if (lines.Count == 0)
        {
            return OrderErrors.NoLines;
        }

        return new Order(Guid.NewGuid(), customerId.Trim(), placedAt, lines);
    }
}
