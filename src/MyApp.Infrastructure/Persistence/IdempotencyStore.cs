using Microsoft.EntityFrameworkCore;
using MyApp.Application.Idempotency;
using MyApp.Application.Ports.Out;

namespace MyApp.Infrastructure.Persistence;

internal sealed class IdempotencyStore(AppDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);

    public void Add(IdempotencyRecord record) => db.IdempotencyRecords.Add(record);
}
