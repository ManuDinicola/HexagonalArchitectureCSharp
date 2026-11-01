using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// Deletes what is no longer needed: idempotency records older than the retention (a client retrying later than that gets a second order),
/// and outbox messages that were delivered longer ago than the retention. Dead-lettered messages are never deleted: a person has to look at them.
/// </summary>
internal sealed partial class RetentionPurger(AppDbContext db, TimeProvider time, TimeSpan retention, ILogger<RetentionPurger> logger)
{
    /// <summary>Deletes everything past the retention.</summary>
    /// <returns>How many idempotency records and outbox messages were deleted.</returns>
    public async Task<(int IdempotencyRecords, int OutboxMessages)> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow() - retention;

        var records = await db.IdempotencyRecords
            .Where(r => r.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (records + messages > 0)
        {
            LogPurged(records, messages, cutoff);
        }

        return (records, messages);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {IdempotencyRecords} idempotency records and {OutboxMessages} outbox messages older than {Cutoff:O}")]
    private partial void LogPurged(int idempotencyRecords, int outboxMessages, DateTimeOffset cutoff);
}
