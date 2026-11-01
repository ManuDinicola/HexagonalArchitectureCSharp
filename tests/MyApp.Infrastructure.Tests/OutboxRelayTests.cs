using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Infrastructure.Tests;

/// <summary>The whole chain as the app wires it: migrations, unit of work, outbox, background relay, publisher.</summary>
public sealed class OutboxRelayTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"myapp-relay-{Guid.CreateVersion7()}.db");

    [Fact]
    public async Task A_saved_order_is_published_by_the_background_relay()
    {
        var publisher = new RecordingPublisher();
        var services = new ServiceCollection().AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddSingleton<IMessagePublisher>(publisher); // the host's broker adapter wins over the default logging one
        services.AddInfrastructure($"Data Source={_file}", outboxPollInterval: TimeSpan.FromMilliseconds(20));
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToList();
        foreach (var service in hosted)
        {
            await service.StartAsync(CancellationToken.None); // registration order: the migration first, then the relay
        }

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var order = Order.Place(Guid.CreateVersion7(DateTimeOffset.UnixEpoch), "cust-1", [new OrderLineDraft("SKU-1", 1, 1m)], DateTimeOffset.UnixEpoch).Unwrap();
                scope.ServiceProvider.GetRequiredService<IOrderRepository>().Add(order);
                (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (publisher.Published.Count == 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            publisher.Published.ShouldHaveSingleItem().Type.ShouldBe("order.placed.v1");
        }
        finally
        {
            foreach (var service in hosted)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }
}
