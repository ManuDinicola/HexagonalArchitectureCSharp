using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Application.ReadModels;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OrderReadStore(AppDbContext db) : IOrderReadStore
{
    public async Task<IReadOnlyList<OrderSummary>> ListNewestFirstAsync(int skip, int take, CancellationToken cancellationToken)
    {
        // Ids are time-ordered (UUIDv7), so ordering by Id is ordering by creation. SQLite cannot ORDER BY a DateTimeOffset.
        // SQLite cannot SUM a decimal either, so the total is computed from the projected lines.
        var rows = await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Select(o => new
            {
                o.Id,
                o.CustomerId,
                o.PlacedAt,
                Lines = o.Lines.Select(l => new { l.Quantity, l.UnitPrice }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new OrderSummary(r.Id, r.CustomerId, r.PlacedAt, r.Lines.Sum(l => l.Quantity * l.UnitPrice), r.Lines.Count))];
    }
}
