using MyApp.Application.Idempotency;

namespace MyApp.Application.Ports.Out;

/// <summary>Remembers the outcome of idempotent commands. Like a repository it only stages: <see cref="IUnitOfWork"/> saves the record in the same transaction as the command's own changes.</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken);

    void Add(IdempotencyRecord record);
}
