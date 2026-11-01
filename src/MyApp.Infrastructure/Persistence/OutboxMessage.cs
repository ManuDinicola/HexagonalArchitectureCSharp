using System.Text.Json;
using MyApp.Domain.Common;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// A domain event waiting to leave the process. Written in the same transaction as the aggregate that raised it, so the event exists if and only if the change was saved.
/// <see cref="Sequence"/> is the delivery order; <see cref="MessageId"/> is what consumers use to ignore duplicates.
/// </summary>
internal sealed class OutboxMessage
{
    public const int TypeMaxLength = 200;
    public const int ErrorMaxLength = 1000;

    /// <summary>After this many failed deliveries the message is dead-lettered: it stays in the table, is no longer retried, and is logged as an error.</summary>
    public const int MaxAttempts = 5;

    private OutboxMessage() { } // EF Core

    public long Sequence { get; init; }
    public Guid MessageId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public bool IsDeadLettered => ProcessedAt is null && Attempts >= MaxAttempts;

    public static OutboxMessage From(IDomainEvent domainEvent, Guid messageId, DateTimeOffset createdAt) => new()
    {
        MessageId = messageId,
        Type = domainEvent.Type,
        Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
        OccurredAt = domainEvent.OccurredAt,
        CreatedAt = createdAt,
    };

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
    }
}
