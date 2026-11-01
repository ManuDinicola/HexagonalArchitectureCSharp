using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Application.Ports.Out;

/// <summary>Storage of <see cref="Order"/>. Stages changes only; <see cref="IUnitOfWork"/> commits them.</summary>
public interface IOrderRepository
{
    void Add(Order order);

    /// <summary>Tracked load, for commands that change the order.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Untracked load, for queries.</summary>
    Task<Order?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
}
