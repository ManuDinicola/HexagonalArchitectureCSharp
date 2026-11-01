using Microsoft.Extensions.Logging;

namespace MyApp.Application.Tests.Fakes;

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
