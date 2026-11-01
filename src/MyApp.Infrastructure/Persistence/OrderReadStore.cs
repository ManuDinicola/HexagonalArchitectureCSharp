using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Application.ReadModels;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OrderReadStore(AppDbContext db) : IOrderReadStore
{
    public async Task<IReadOnlyList<OrderSummary>> ListNewestFirstAsync(int skip, int take, CancellationToken cancellationToken)
    {
        // Ids are time-ordered (UUIDv7), so ordering by Id is ordering by creation. SQLite cannot ORDER BY a DateTimeOffset.
        // The total is the one the aggregate computed and stored: nothing is recomputed here.
        var rows = await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Select(o => new { o.Id, o.CustomerId, o.PlacedAt, o.Total, LineCount = o.Lines.Count, o.Status })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new OrderSummary(r.Id, r.CustomerId, r.PlacedAt, r.Total, r.LineCount, r.Status.ToString()))];
    }
}
