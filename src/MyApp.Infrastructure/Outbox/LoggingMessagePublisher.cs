using Microsoft.Extensions.Logging;
using MyApp.Application.Outbox;
using MyApp.Application.Ports.Out;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// The default adapter of <see cref="IMessagePublisher"/>: it only logs, so the template runs without a broker.
/// Replace it with your own (Service Bus, RabbitMQ, Kafka, a webhook): register yours before AddInfrastructure() and this one steps aside.
/// </summary>
internal sealed partial class LoggingMessagePublisher(ILogger<LoggingMessagePublisher> logger) : IMessagePublisher
{
    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        LogPublished(message.Type, message.MessageId, message.Payload);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Type} {MessageId}: {Payload}")]
    private partial void LogPublished(string type, Guid messageId, string payload);
}
