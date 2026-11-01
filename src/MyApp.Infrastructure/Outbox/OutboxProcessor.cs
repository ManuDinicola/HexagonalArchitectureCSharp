using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Outbox;
using MyApp.Application.Ports.Out;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// Publishes one batch of pending messages, oldest first, and marks each as processed. Delivery is at least once: a crash between
/// publishing and saving sends the message again, so consumers must ignore a MessageId they have seen.
/// Order is kept: the first failure ends the batch (a later message never overtakes an earlier one). A message that keeps failing is
/// dead-lettered after <see cref="OutboxMessage.MaxAttempts"/> tries so it cannot block the rest forever.
/// </summary>
internal sealed partial class OutboxProcessor(AppDbContext db, IMessagePublisher publisher, TimeProvider time, ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;

    /// <summary>Publishes the next batch.</summary>
    /// <returns>How many messages were published.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < OutboxMessage.MaxAttempts)
            .OrderBy(m => m.Sequence)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(new OutgoingMessage(message.MessageId, message.Type, message.Payload, message.OccurredAt), cancellationToken);
                message.MarkProcessed(time.GetUtcNow());
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.Message);
                if (message.IsDeadLettered)
                {
                    LogDeadLettered(ex, message.Type, message.MessageId, message.Attempts);
                }
                else
                {
                    LogFailed(ex, message.Type, message.MessageId, message.Attempts);
                }

                break;
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another relay instance handled the same messages: they were delivered twice at worst, which at-least-once allows.
            LogRaced(ex);
            return 0;
        }

        return published;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {Type} {MessageId} failed (attempt {Attempts}); it will be retried")]
    private partial void LogFailed(Exception exception, string type, Guid messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing {Type} {MessageId} failed {Attempts} times: dead-lettered, it will not be retried")]
    private partial void LogDeadLettered(Exception exception, string type, Guid messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Another outbox relay updated the same messages; this batch is dropped")]
    private partial void LogRaced(Exception exception);
}
