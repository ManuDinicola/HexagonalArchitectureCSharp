using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly RecordingPublisher _publisher = new();

    private async Task PlaceOrdersAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var at = DateTimeOffset.UnixEpoch.AddMinutes(i);
            var order = Order.Place(Guid.CreateVersion7(at), $"cust-{i}", [new OrderLineDraft("SKU-1", 1, 1m)], at).Unwrap();
            await using var db = _database.NewContext();
            new OrderRepository(db).Add(order);
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<int> RunAsync()
    {
        await using var db = _database.NewContext();
        return await new OutboxProcessor(db, _publisher, TimeProvider.System, NullLogger<OutboxProcessor>.Instance)
            .ProcessBatchAsync(CancellationToken.None);
    }

    private async Task<List<OutboxMessage>> MessagesAsync()
    {
        await using var db = _database.NewContext();
        return await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync();
    }

    [Fact]
    public async Task Pending_messages_are_published_oldest_first_and_marked_processed()
    {
        await PlaceOrdersAsync(3);

        (await RunAsync()).ShouldBe(3);

        var stored = await MessagesAsync();
        _publisher.Published.Select(m => m.MessageId).ShouldBe(stored.Select(m => m.MessageId));
        stored.ShouldAllBe(m => m.ProcessedAt != null && m.Attempts == 0);
    }

    [Fact]
    public async Task What_is_published_is_what_was_stored()
    {
        await PlaceOrdersAsync(1);

        await RunAsync();

        var stored = (await MessagesAsync()).ShouldHaveSingleItem();
        var sent = _publisher.Published.ShouldHaveSingleItem();
        sent.MessageId.ShouldBe(stored.MessageId);
        sent.Type.ShouldBe("order.placed.v1");
        sent.Payload.ShouldBe(stored.Payload);
        sent.OccurredAt.ShouldBe(stored.OccurredAt);
    }

    [Fact]
    public async Task A_processed_message_is_not_published_again()
    {
        await PlaceOrdersAsync(2);
        await RunAsync();

        (await RunAsync()).ShouldBe(0);

        _publisher.Attempted.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_failure_stops_the_batch_so_order_is_kept_and_the_next_run_resumes()
    {
        await PlaceOrdersAsync(3);
        var second = (await MessagesAsync())[1].MessageId;
        _publisher.Fails = m => m.MessageId == second;

        (await RunAsync()).ShouldBe(1); // the first went out; the second failed; the third was not even tried

        var afterFailure = await MessagesAsync();
        afterFailure[0].ProcessedAt.ShouldNotBeNull();
        afterFailure[1].Attempts.ShouldBe(1);
        afterFailure[1].LastError.ShouldBe("The broker is down.");
        afterFailure[2].Attempts.ShouldBe(0);
        _publisher.Attempted.Count.ShouldBe(2);

        _publisher.Fails = _ => false;
        (await RunAsync()).ShouldBe(2);

        _publisher.Published.Select(m => m.MessageId).ShouldBe((await MessagesAsync()).Select(m => m.MessageId));
        (await MessagesAsync()).ShouldAllBe(m => m.ProcessedAt != null);
    }

    [Fact]
    public async Task A_message_that_keeps_failing_is_dead_lettered_and_stops_blocking_the_others()
    {
        await PlaceOrdersAsync(2);
        var poison = (await MessagesAsync())[0].MessageId;
        _publisher.Fails = m => m.MessageId == poison;

        for (var attempt = 0; attempt < OutboxMessage.MaxAttempts; attempt++)
        {
            (await RunAsync()).ShouldBe(0);
        }

        (await MessagesAsync())[0].IsDeadLettered.ShouldBeTrue();

        (await RunAsync()).ShouldBe(1); // the healthy one finally goes out
        _publisher.Attempted.Count(m => m.MessageId == poison).ShouldBe(OutboxMessage.MaxAttempts); // and the poison one is not retried
        (await MessagesAsync())[0].ProcessedAt.ShouldBeNull();
    }

    [Fact]
    public async Task One_run_sends_at_most_a_batch()
    {
        await PlaceOrdersAsync(OutboxProcessor.BatchSize + 1);

        (await RunAsync()).ShouldBe(OutboxProcessor.BatchSize);
        (await RunAsync()).ShouldBe(1);
    }

    public void Dispose() => _database.Dispose();
}
