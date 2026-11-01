using MyApp.Application.Outbox;
using MyApp.Application.Ports.Out;

namespace MyApp.Infrastructure.Tests;

/// <summary>A publisher that remembers what it was given and can be told to fail, like a broker that is down.</summary>
internal sealed class RecordingPublisher : IMessagePublisher
{
    private readonly List<OutgoingMessage> _published = [];
    private readonly List<OutgoingMessage> _attempted = [];

    /// <summary>Messages that were accepted, in order.</summary>
    public IReadOnlyList<OutgoingMessage> Published
    {
        get
        {
            lock (_published)
            {
                return [.. _published];
            }
        }
    }

    /// <summary>Every call, including the ones that failed.</summary>
    public IReadOnlyList<OutgoingMessage> Attempted
    {
        get
        {
            lock (_attempted)
            {
                return [.. _attempted];
            }
        }
    }

    /// <summary>When it returns true for a message, publishing that message throws.</summary>
    public Func<OutgoingMessage, bool> Fails { get; set; } = _ => false;

    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        lock (_attempted)
        {
            _attempted.Add(message);
        }

        if (Fails(message))
        {
            throw new InvalidOperationException("The broker is down.");
        }

        lock (_published)
        {
            _published.Add(message);
        }

        return Task.CompletedTask;
    }
}
