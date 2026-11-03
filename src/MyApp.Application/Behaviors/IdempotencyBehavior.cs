using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyApp.Application.Idempotency;
using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

/// <summary>
/// Innermost step: runs inside <see cref="UnitOfWorkBehavior{TRequest, TResponse}"/>, so the record is saved atomically with the command's changes.
/// A retry with the same key and the same request replays the recorded value. The same key with another request is a conflict.
/// Two concurrent first calls both try to insert the record: the loser hits the unique key, and the whole command rolls back as a Conflict.
/// </summary>
internal sealed class IdempotencyBehavior<TRequest, TValue>(IIdempotencyStore store, TimeProvider time)
    : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : IIdempotentCommand<TValue>
{
    public async Task<Result<TValue>> HandleAsync(TRequest request, RequestHandlerDelegate<Result<TValue>> next, CancellationToken cancellationToken)
    {
        var key = request.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return await next();
        }

        var scope = typeof(TRequest).Name;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

        var existing = await store.FindAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal)
                ? Result.Success(JsonSerializer.Deserialize<TValue>(existing.Response)
                    ?? throw new InvalidOperationException($"The recorded response of '{key}' is empty."))
                : IdempotencyErrors.KeyReused;
        }

        var response = await next();
        if (response is TValue value)
        {
            store.Add(new IdempotencyRecord(scope, key, fingerprint, JsonSerializer.Serialize(value), time.GetUtcNow()));
        }

        return response;
    }
}
