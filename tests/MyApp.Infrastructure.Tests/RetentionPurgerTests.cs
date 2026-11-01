using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Application.Idempotency;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class RetentionPurgerTests : IDisposable
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(Start);

    private async Task SaveOrderAsync(string key)
    {
        var order = Order.Place(Guid.CreateVersion7(_time.GetUtcNow()), "cust-1", [new OrderLineDraft("SKU-1", 1, 1m)], _time.GetUtcNow()).Unwrap();
        await using var db = _database.NewContext(_time);
        new OrderRepository(db).Add(order);
        new IdempotencyStore(db).Add(new IdempotencyRecord("PlaceOrderCommand", key, new string('A', 64), "\"x\"", _time.GetUtcNow()));
        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    private async Task<(int Idempotency, int Outbox)> PurgeAsync()
    {
        await using var db = _database.NewContext(_time);
        return await new RetentionPurger(db, _time, Retention, NullLogger<RetentionPurger>.Instance).PurgeAsync(CancellationToken.None);
    }

    private async Task DeliverAllAsync()
    {
        await using var db = _database.NewContext(_time);
        await new OutboxProcessor(db, new RecordingPublisher(), _time, NullLogger<OutboxProcessor>.Instance).ProcessBatchAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Nothing_is_deleted_before_the_retention_has_passed()
    {
        await SaveOrderAsync("key-1");
        await DeliverAllAsync();
        _time.Advance(Retention - TimeSpan.FromMinutes(1));

        (await PurgeAsync()).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Old_idempotency_records_and_delivered_messages_are_deleted_and_the_order_is_kept()
    {
        await SaveOrderAsync("key-1");
        await DeliverAllAsync();
        _time.Advance(Retention + TimeSpan.FromMinutes(1));

        (await PurgeAsync()).ShouldBe((1, 1));

        await using var db = _database.NewContext();
        (await db.IdempotencyRecords.CountAsync()).ShouldBe(0);
        (await db.OutboxMessages.CountAsync()).ShouldBe(0);
        (await db.Orders.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Recent_records_survive_while_old_ones_go()
    {
        await SaveOrderAsync("old");
        await DeliverAllAsync();
        _time.Advance(Retention + TimeSpan.FromMinutes(1));
        await SaveOrderAsync("new");
        await DeliverAllAsync();

        (await PurgeAsync()).ShouldBe((1, 1));

        await using var db = _database.NewContext();
        (await db.IdempotencyRecords.SingleAsync()).Key.ShouldBe("new");
        (await db.OutboxMessages.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Undelivered_and_dead_lettered_messages_are_never_purged()
    {
        await SaveOrderAsync("key-1"); // never delivered
        _time.Advance(Retention * 10);

        (await PurgeAsync()).Outbox.ShouldBe(0);

        await using var db = _database.NewContext();
        (await db.OutboxMessages.CountAsync()).ShouldBe(1);
    }

    public void Dispose() => _database.Dispose();
}
