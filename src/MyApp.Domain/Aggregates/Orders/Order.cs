using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public sealed class Order
{
    public const int CustomerIdMaxLength = 100;

    private readonly List<OrderLine> _lines = [];
    private IReadOnlyList<OrderLine>? _linesView;

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
    public IReadOnlyList<OrderLine> Lines => _linesView ??= _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.Total);

    /// <summary>
    /// The only way to create an order, so invariants cannot be skipped. Reports every broken rule at once;
    /// line errors are targeted at "lines[i].field". The id and the time come from the caller: the Domain never generates them.
    /// </summary>
    public static Result<Order> Place(Guid id, string customerId, IReadOnlyList<OrderLineDraft> lines, DateTimeOffset placedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An order needs a real id.", nameof(id)); // a bug in the caller, not an expected failure
        }

        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(customerId))
        {
            errors.Add(OrderErrors.CustomerRequired);
        }
        else if (customerId.Trim().Length > CustomerIdMaxLength)
        {
            errors.Add(OrderErrors.CustomerTooLong);
        }

        if (lines.Count == 0)
        {
            errors.Add(OrderErrors.NoLines);
        }

        var created = new List<OrderLine>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = OrderLine.Create(lines[i].Sku, lines[i].Quantity, lines[i].UnitPrice);
            if (line.IsSuccess)
            {
                created.Add(line.Unwrap());
            }
            else
            {
                var index = i;
                errors.AddRange(line.Errors.Select(e => e with { Target = $"lines[{index}].{e.Target}" }));
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure<Order>(errors);
        }

        return new Order(id, customerId.Trim(), placedAt, created);
    }
}
