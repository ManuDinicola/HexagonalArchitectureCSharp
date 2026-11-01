namespace MyApp.Application.Outbox;

/// <summary>A message on its way out of the process, as the outbox hands it to the publisher.</summary>
/// <param name="MessageId">Unique and stable across retries: consumers use it to ignore a message they already handled (delivery is at least once).</param>
/// <param name="Type">The versioned event name, for example <c>order.placed.v1</c>.</param>
/// <param name="Payload">The event as JSON.</param>
/// <param name="OccurredAt">When the event happened in the domain.</param>
public sealed record OutgoingMessage(Guid MessageId, string Type, string Payload, DateTimeOffset OccurredAt);
