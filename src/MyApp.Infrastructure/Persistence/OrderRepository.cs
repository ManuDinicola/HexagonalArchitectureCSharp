using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
