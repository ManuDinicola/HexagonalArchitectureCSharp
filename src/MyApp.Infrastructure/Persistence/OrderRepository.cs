using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
