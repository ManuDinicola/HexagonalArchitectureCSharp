using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

public sealed class Order : IHasDomainEvents
{
    public const int CustomerIdMaxLength = 100;

    private readonly List<OrderLine> _lines = [];
    private IReadOnlyList<OrderLine>? _linesView;
    private readonly List<IDomainEvent> _domainEvents = [];

    private Order() { } // EF Core

    private Order(Guid id, string customerId, DateTimeOffset placedAt, IEnumerable<OrderLine> lines)
    {
        Id = id;
        CustomerId = customerId;
        PlacedAt = placedAt;
        _lines.AddRange(lines);
        Total = _lines.Sum(l => l.Total); // the one place the total is computed; it is stored, so readers never recompute it
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = null!;
    public DateTimeOffset PlacedAt { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _linesView ??= _lines.AsReadOnly();
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Placed;
    public DateTimeOffset? CancelledAt { get; private set; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

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
            if (line is OrderLine valid)
            {
                created.Add(valid);
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

        var order = new Order(id, customerId.Trim(), placedAt, created);
        order._domainEvents.Add(new OrderPlaced(order.Id, order.CustomerId, order.Total, created.Count, placedAt));
        return order;
    }

    /// <summary>A placed order can be cancelled once. Cancelling again is a conflict, not a bug. The time comes from the caller.</summary>
    public Result Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == OrderStatus.Cancelled)
        {
            return OrderErrors.AlreadyCancelled(Id);
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = cancelledAt;
        _domainEvents.Add(new OrderCancelled(Id, cancelledAt));
        return Result.Success();
    }
}
