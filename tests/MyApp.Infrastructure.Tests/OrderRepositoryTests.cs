using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

/// <summary>Real EF Core against in-memory SQLite. The connection must stay open or the database disappears.</summary>
public sealed class OrderRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public OrderRepositoryTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Order_round_trips_with_its_lines()
    {
        var placedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var order = Order.Place("cust-1",
            [OrderLine.Create("SKU-1", 2, 10.5m).Value, OrderLine.Create("SKU-2", 1, 3m).Value],
            placedAt).Value;

        await using (var write = NewContext())
            await new OrderRepository(write).AddAsync(order, CancellationToken.None);

        await using var read = NewContext(); // fresh context: nothing comes from the change tracker
        var loaded = await new OrderRepository(read).GetByIdAsync(order.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("cust-1", loaded.CustomerId);
        Assert.Equal(placedAt, loaded.PlacedAt);
        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal(24m, loaded.Total);
    }

    [Fact]
    public async Task Unknown_id_returns_null()
    {
        await using var db = NewContext();

        Assert.Null(await new OrderRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AddInfrastructure_wires_every_port()
    {
        var services = new ServiceCollection().AddInfrastructure("DataSource=:memory:");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrderRepository>());
        Assert.True((DateTimeOffset.UnixEpoch < scope.ServiceProvider.GetRequiredService<IClock>().UtcNow));
    }

    public void Dispose() => _connection.Dispose();
}
