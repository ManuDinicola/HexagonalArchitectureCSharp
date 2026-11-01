using MyApp.Application.Outbox;

namespace MyApp.Application.Ports.Out;

/// <summary>
/// Sends a message to the outside world (a broker, a webhook). Only the outbox relay calls it, after the event was saved with the business change.
/// Throw to say "not delivered": the relay retries, so the same <see cref="OutgoingMessage.MessageId"/> can arrive more than once.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
