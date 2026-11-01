using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

/// <summary>Several copies of the app on one database: what they share must come out right.</summary>
public sealed class MultiInstanceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"myapp-instances-{Guid.CreateVersion7()}.db");
    private readonly RecordingPublisher _broker = new();

    private ServiceProvider NewInstance(bool backgroundJobs = true, TimeSpan? retention = null)
    {
        var services = new ServiceCollection().AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddSingleton<IMessagePublisher>(_broker); // the same broker for every instance
        services.AddInfrastructure($"Data Source={_file}", runBackgroundJobs: backgroundJobs, outboxPollInterval: TimeSpan.FromMilliseconds(20), retention: retention);
        return services.BuildServiceProvider();
    }

    private static async Task StartAsync(ServiceProvider instance)
    {
        foreach (var service in instance.GetServices<IHostedService>())
        {
            await service.StartAsync(CancellationToken.None);
        }
    }

    private static async Task StopAsync(ServiceProvider instance)
    {
        foreach (var service in instance.GetServices<IHostedService>())
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task PlaceOrdersAsync(ServiceProvider instance, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await using var scope = instance.CreateAsyncScope();
            var at = DateTimeOffset.UtcNow;
            var order = Order.Place(Guid.CreateVersion7(at), $"cust-{i}", [new OrderLineDraft("SKU-1", 1, 1m)], at).Unwrap();
            scope.ServiceProvider.GetRequiredService<IOrderRepository>().Add(order);
            (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Two_instances_publish_every_message_exactly_once_and_in_order()
    {
        await using var first = NewInstance();
        await using var second = NewInstance();
        await StartAsync(first); // migrates
        await StartAsync(second);
        try
        {
            await PlaceOrdersAsync(first, 15);
            await PlaceOrdersAsync(second, 15);
            await WaitUntilAsync(() => _broker.Published.Count >= 30);
            await Task.Delay(300); // time for a second instance to wrongly publish a duplicate, if it could

            var stored = await StoredMessageIdsAsync(first);
            _broker.Published.Count.ShouldBe(30);
            _broker.Published.Select(m => m.MessageId).ShouldBe(stored); // every message once, in the order they were saved
        }
        finally
        {
            await StopAsync(first);
            await StopAsync(second);
        }
    }

    [Fact]
    public async Task Only_one_instance_holds_the_relay_lease_at_a_time()
    {
        await using var first = NewInstance();
        await using var second = NewInstance();
        await StartAsync(first);
        await StartAsync(second);
        try
        {
            await WaitUntilAsync(() => LeaseCount(first) == 2); // relay + retention each have a holder
            await Task.Delay(200);

            await using var scope = first.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.RelayLeases.AsNoTracking().Select(l => l.Name).OrderBy(n => n).ToListAsync()).ShouldBe(["outbox-relay", "retention"]);
        }
        finally
        {
            await StopAsync(first);
            await StopAsync(second);
        }
    }

    [Fact]
    public async Task The_retention_worker_deletes_delivered_messages_and_old_idempotency_records()
    {
        await using var instance = NewInstance(backgroundJobs: false, retention: TimeSpan.FromMilliseconds(1));
        await StartAsync(instance); // only the migration runs
        await PlaceOrdersAsync(instance, 2);
        await using (var scope = instance.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.IdempotencyRecords.Add(new Application.Idempotency.IdempotencyRecord("S", "K", new string('A', 64), "R", DateTimeOffset.UtcNow.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        await Task.Delay(20);
        var worker = new RetentionWorker(
            instance.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(5), "worker", NullLogger<RetentionWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => CountOf(instance) == (0, 0));

            CountOf(instance).ShouldBe((0, 0));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<List<Guid>> StoredMessageIdsAsync(ServiceProvider instance)
    {
        await using var scope = instance.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).Select(m => m.MessageId).ToListAsync();
    }

    private static int LeaseCount(ServiceProvider instance)
    {
        using var scope = instance.CreateScope();
        try
        {
            return scope.ServiceProvider.GetRequiredService<AppDbContext>().RelayLeases.AsNoTracking().Count();
        }
        catch (SqliteException)
        {
            return 0; // the schema is not there yet
        }
    }

    private static (int Idempotency, int Outbox) CountOf(ServiceProvider instance)
    {
        using var scope = instance.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (db.IdempotencyRecords.AsNoTracking().Count(), db.OutboxMessages.AsNoTracking().Count());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }
}
