namespace MyApp.Domain.Common;

/// <summary>
/// Something that happened in the domain, named in the past tense. Raised by an aggregate, published reliably through the outbox.
/// Implement it on a <c>readonly record struct</c> in the aggregate's folder; the time is a parameter, the Domain never reads the clock.
/// </summary>
public interface IDomainEvent
{
    /// <summary>The stable, versioned name consumers rely on (for example <c>order.placed.v1</c>). Never rename it: add a new version.</summary>
    string Type { get; }

    DateTimeOffset OccurredAt { get; }
}
