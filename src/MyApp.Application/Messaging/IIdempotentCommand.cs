using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>
/// A command that can be retried safely. With a key, the first success is recorded and a retry gets the same answer without running the handler again.
/// Without a key it behaves like any command. Implement it on commands that create something.
/// </summary>
public interface IIdempotentCommand<TValue> : ICommand<Result<TValue>>
{
    string? IdempotencyKey { get; }
}
