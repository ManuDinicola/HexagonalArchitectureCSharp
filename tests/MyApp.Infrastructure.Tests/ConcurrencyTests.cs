using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private async Task<Guid> SeedOrderAsync()
    {
        var order = Order.Place(Guid.CreateVersion7(DateTimeOffset.UnixEpoch), "cust-1", [new OrderLineDraft("SKU-1", 1, 1m)], DateTimeOffset.UnixEpoch).Unwrap();
        await using var db = _database.NewContext();
        new OrderRepository(db).Add(order);
        await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
        return order.Id;
    }

    private static async Task<Order> LoadAsync(AppDbContext db, Guid id) =>
        (await new OrderRepository(db).GetByIdAsync(id, CancellationToken.None))!;

    private static Guid VersionOf(AppDbContext db, Order order) =>
        (Guid)db.Entry(order).Property(ConcurrencyToken.PropertyName).CurrentValue!;

    [Fact]
    public void Every_aggregate_root_has_a_concurrency_token()
    {
        using var db = _database.NewContext();

        var roots = db.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList();

        roots.ShouldNotBeEmpty();
        foreach (var root in roots)
        {
            root.FindProperty(ConcurrencyToken.PropertyName)?.IsConcurrencyToken.ShouldBeTrue(
                $"{root.ClrType.Name}: call builder.UseConcurrencyToken() in its IEntityTypeConfiguration.");
        }
    }

    [Fact]
    public async Task A_new_order_gets_a_version()
    {
        var id = await SeedOrderAsync();

        await using var db = _database.NewContext();
        VersionOf(db, await LoadAsync(db, id)).ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Two_writers_of_the_same_order_cannot_both_win()
    {
        var id = await SeedOrderAsync();
        await using var first = _database.NewContext();
        await using var second = _database.NewContext();
        var a = await LoadAsync(first, id);
        var b = await LoadAsync(second, id);
        first.Entry(a).Property(o => o.CustomerId).CurrentValue = "from-first";
        second.Entry(b).Property(o => o.CustomerId).CurrentValue = "from-second";

        (await new UnitOfWork(first).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        var result = await new UnitOfWork(second).SaveChangesAsync(CancellationToken.None);

        result.Errors.ShouldHaveSingleItem().ShouldBe(PersistenceErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task Changing_only_a_line_still_changes_the_orders_version()
    {
        var id = await SeedOrderAsync();
        await using var reader = _database.NewContext();
        var before = VersionOf(reader, await LoadAsync(reader, id));

        await using (var writer = _database.NewContext())
        {
            var order = await LoadAsync(writer, id);
            writer.Entry(order.Lines[0]).Property(l => l.Quantity).CurrentValue = 5;
            (await new UnitOfWork(writer).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        await using var after = _database.NewContext();
        VersionOf(after, await LoadAsync(after, id)).ShouldNotBe(before);
    }

    public void Dispose() => _database.Dispose();
}
