using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class OrderRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private static Order NewOrder(DateTimeOffset placedAt) =>
        Order.Place(Guid.CreateVersion7(placedAt), "cust-1",
            [new OrderLineDraft("SKU-1", 2, 10.5m), new OrderLineDraft("SKU-2", 1, 3m)], placedAt).Unwrap();

    [Fact]
    public async Task Order_round_trips_with_its_lines()
    {
        var placedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var order = NewOrder(placedAt);

        await using (var write = _database.NewContext())
        {
            new OrderRepository(write).Add(order);
            (await new UnitOfWork(write).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        await using var read = _database.NewContext();
        var loaded = await new OrderRepository(read).GetByIdReadOnlyAsync(order.Id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.CustomerId.ShouldBe("cust-1");
        loaded.PlacedAt.ShouldBe(placedAt);
        loaded.Lines.Count.ShouldBe(2);
        loaded.Total.ShouldBe(24m);
        read.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Tracked_read_returns_the_order_for_commands_that_change_it()
    {
        var order = NewOrder(DateTimeOffset.UnixEpoch);
        await using (var write = _database.NewContext())
        {
            new OrderRepository(write).Add(order);
            await new UnitOfWork(write).SaveChangesAsync(CancellationToken.None);
        }

        await using var read = _database.NewContext();
        var loaded = await new OrderRepository(read).GetByIdAsync(order.Id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        read.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Unknown_id_returns_null()
    {
        await using var db = _database.NewContext();

        (await new OrderRepository(db).GetByIdReadOnlyAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await new OrderRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Saving_the_same_order_twice_is_a_conflict_result_not_an_exception()
    {
        var order = NewOrder(DateTimeOffset.UnixEpoch);
        await using (var first = _database.NewContext())
        {
            new OrderRepository(first).Add(order);
            await new UnitOfWork(first).SaveChangesAsync(CancellationToken.None);
        }

        await using var second = _database.NewContext();
        new OrderRepository(second).Add(order);
        var result = await new UnitOfWork(second).SaveChangesAsync(CancellationToken.None);

        result.Errors.ShouldHaveSingleItem().ShouldBe(PersistenceErrors.DuplicateKey);
    }

    [Fact]
    public async Task AddInfrastructure_wires_every_port()
    {
        var services = new ServiceCollection().AddInfrastructure("DataSource=:memory:");
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();

        var ports = typeof(IOrderRepository).Assembly.GetTypes()
            .Where(t => t.IsInterface && string.Equals(t.Namespace, typeof(IOrderRepository).Namespace, StringComparison.Ordinal));

        foreach (var port in ports)
        {
            scope.ServiceProvider.GetRequiredService(port).ShouldNotBeNull(port.Name);
        }
    }

    public void Dispose() => _database.Dispose();
}
