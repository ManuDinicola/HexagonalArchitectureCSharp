using MyApp.Domain.Common;

namespace MyApp.Infrastructure.Persistence;

/// <summary>Expected storage failures, translated from provider exceptions so callers see a Result, not an EF/SQLite type.</summary>
internal static class PersistenceErrors
{
    public static Error DuplicateKey { get; } =
        Error.Conflict("Persistence.DuplicateKey", "A record with the same key already exists.");

    public static Error ConcurrencyConflict { get; } =
        Error.Conflict("Persistence.ConcurrencyConflict", "The data was changed by someone else. Reload and try again.");
}
