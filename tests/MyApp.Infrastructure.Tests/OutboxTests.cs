using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

/// <summary>Events leave the aggregate and enter the outbox in the same transaction as the change, or not at all.</summary>
public sealed class OutboxTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private static Order NewOrder(Guid? id = null) =>
        Order.Place(id ?? Guid.CreateVersion7(DateTimeOffset.UnixEpoch), "cust-1", [new OrderLineDraft("SKU-1", 2, 5m)], DateTimeOffset.UnixEpoch).Unwrap();

    private async Task<List<OutboxMessage>> MessagesAsync()
    {
        await using var db = _database.NewContext();
        return await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync();
    }

    [Fact]
    public async Task Saving_a_new_order_writes_its_event_to_the_outbox_and_clears_it_from_the_aggregate()
    {
        var order = NewOrder();
        await using (var db = _database.NewContext())
        {
            new OrderRepository(db).Add(order);
            (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        var message = (await MessagesAsync()).ShouldHaveSingleItem();
        message.Type.ShouldBe("order.placed.v1");
        message.ProcessedAt.ShouldBeNull();
        message.Attempts.ShouldBe(0);
        message.MessageId.Version.ShouldBe(7);
        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.GetProperty("OrderId").GetGuid().ShouldBe(order.Id);
        payload.RootElement.GetProperty("CustomerId").GetString().ShouldBe("cust-1");
        payload.RootElement.GetProperty("Total").GetDecimal().ShouldBe(10m);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_later_change_adds_its_own_message_after_the_first()
    {
        var order = NewOrder();
        await using (var db = _database.NewContext())
        {
            new OrderRepository(db).Add(order);
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = _database.NewContext())
        {
            var tracked = (await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None)).ShouldNotBeNull();
            tracked.Cancel(DateTimeOffset.UnixEpoch.AddHours(1)).IsSuccess.ShouldBeTrue();
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }

        var messages = await MessagesAsync();
        messages.Select(m => m.Type).ShouldBe(["order.placed.v1", "order.cancelled.v1"]);
        messages[1].Sequence.ShouldBeGreaterThan(messages[0].Sequence);
    }

    [Fact]
    public async Task A_save_that_fails_leaves_no_message_behind()
    {
        var id = Guid.CreateVersion7(DateTimeOffset.UnixEpoch);
        await using (var db = _database.NewContext())
        {
            new OrderRepository(db).Add(NewOrder(id));
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = _database.NewContext())
        {
            new OrderRepository(db).Add(NewOrder(id)); // same id: duplicate key
            var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
            result.IsFailure.ShouldBeTrue();
        }

        (await MessagesAsync()).ShouldHaveSingleItem(); // only the first order's event
    }

    [Fact]
    public async Task Saving_without_a_new_event_adds_nothing()
    {
        var order = NewOrder();
        await using (var db = _database.NewContext())
        {
            new OrderRepository(db).Add(order);
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = _database.NewContext())
        {
            await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);
            await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        }

        (await MessagesAsync()).ShouldHaveSingleItem();
    }

    public void Dispose() => _database.Dispose();
}
