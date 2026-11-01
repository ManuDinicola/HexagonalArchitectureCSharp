namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// "Who may run this background job right now". One row per job; the holder renews it, and when it stops (crash, deploy) the lease expires and another instance takes over.
/// Two instances racing for the same row cannot both win: the row carries the optimistic concurrency token, and inserting the first row hits the unique key.
/// </summary>
internal sealed class RelayLease
{
    private RelayLease() { } // EF Core

    public RelayLease(string name, string owner, DateTimeOffset expiresAt)
    {
        Name = name;
        Owner = owner;
        ExpiresAt = expiresAt;
    }

    public string Name { get; private set; } = null!;
    public string Owner { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool CanBeTakenBy(string owner, DateTimeOffset now) =>
        string.Equals(Owner, owner, StringComparison.Ordinal) || ExpiresAt <= now;

    public void Take(string owner, DateTimeOffset expiresAt)
    {
        Owner = owner;
        ExpiresAt = expiresAt;
    }
}
