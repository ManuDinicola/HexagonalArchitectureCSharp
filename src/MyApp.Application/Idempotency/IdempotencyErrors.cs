using MyApp.Domain.Common;

namespace MyApp.Application.Idempotency;

public static class IdempotencyErrors
{
    public static Error KeyReused { get; } =
        Error.Conflict("Idempotency.KeyReused", "This idempotency key was already used with a different request.");
}
