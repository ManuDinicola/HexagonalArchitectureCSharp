using MyApp.Application.Idempotency;
using MyApp.Application.Ports.Out;

namespace MyApp.Application.Tests.Fakes;

/// <summary>Like the real store: Add only stages; the record is visible after <see cref="Commit"/> (the unit of work's save).</summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly List<IdempotencyRecord> _staged = [];

    public List<IdempotencyRecord> Saved { get; } = [];

    public Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.FirstOrDefault(r => string.Equals(r.Scope, scope, StringComparison.Ordinal) && string.Equals(r.Key, key, StringComparison.Ordinal)));

    public void Add(IdempotencyRecord record) => _staged.Add(record);

    public void Commit()
    {
        Saved.AddRange(_staged);
        _staged.Clear();
    }
}
