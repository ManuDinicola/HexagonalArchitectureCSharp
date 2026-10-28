using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class OrderReadStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private static Order NewOrder(DateTimeOffset placedAt, string customer, params OrderLineDraft[] lines) =>
        Order.Place(Guid.CreateVersion7(placedAt), customer, lines, placedAt).Unwrap();

    private async Task SeedAsync(params Order[] orders)
    {
        await using var db = _database.NewContext();
        var repository = new OrderRepository(db);
        foreach (var order in orders)
        {
            repository.Add(order);
        }

        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Lists_newest_first_with_total_and_line_count_matching_the_aggregate()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var oldest = NewOrder(t0, "a", new OrderLineDraft("S1", 2, 10.5m), new OrderLineDraft("S2", 1, 3m));
        var newest = NewOrder(t0.AddHours(1), "b", new OrderLineDraft("S1", 1, 1m));
        await SeedAsync(oldest, newest);

        await using var db = _database.NewContext();
        var rows = await new OrderReadStore(db).ListNewestFirstAsync(0, 10, CancellationToken.None);

        rows.Select(r => r.Id).ShouldBe([newest.Id, oldest.Id]);
        rows[1].Total.ShouldBe(oldest.Total);
        rows[1].LineCount.ShouldBe(2);
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Skip_and_take_page_through_the_orders()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var orders = Enumerable.Range(0, 5).Select(i => NewOrder(t0.AddMinutes(i), "c", new OrderLineDraft("S", 1, 1m))).ToArray();
        await SeedAsync(orders);

        await using var db = _database.NewContext();
        var page = await new OrderReadStore(db).ListNewestFirstAsync(2, 2, CancellationToken.None);

        page.Select(r => r.Id).ShouldBe([orders[2].Id, orders[1].Id]);
    }

    public void Dispose() => _database.Dispose();
}
