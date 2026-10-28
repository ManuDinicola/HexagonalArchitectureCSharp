using Microsoft.Extensions.Logging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Orders;

namespace MyApp.Application.Tests.Fakes;

public sealed class FakeOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Stored { get; } = [];

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        Stored[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.GetValueOrDefault(id));
}

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Collects log messages in order, so tests can assert on the pipeline.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    public List<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);

    public void Dispose() { }

    private sealed class ListLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add($"{logLevel}: {formatter(state, exception)}");
    }
}
