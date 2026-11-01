using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// Leader election for background jobs through a row in the database, so it works with any database EF supports and needs no extra infrastructure.
/// Call it before every unit of work: the holder renews, a stranger gets false until the lease runs out.
/// </summary>
internal sealed class LeaderLease(AppDbContext db, IUnitOfWork unitOfWork, TimeProvider time)
{
    /// <summary>Takes or renews the lease.</summary>
    /// <returns>True when <paramref name="owner"/> holds the lease for <paramref name="name"/> until now + <paramref name="duration"/>.</returns>
    public async Task<bool> TryAcquireAsync(string name, string owner, TimeSpan duration, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lease = await db.RelayLeases.FirstOrDefaultAsync(l => l.Name == name, cancellationToken);

        if (lease is null)
        {
            db.RelayLeases.Add(new RelayLease(name, owner, now + duration));
        }
        else if (lease.CanBeTakenBy(owner, now))
        {
            lease.Take(owner, now + duration);
        }
        else
        {
            return false;
        }

        // Two instances inserting the first row, or taking an expired one, collide here (unique key or version): exactly one wins, the other gets a Conflict.
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            db.ChangeTracker.Clear();
        }

        return saved.IsSuccess;
    }
}
