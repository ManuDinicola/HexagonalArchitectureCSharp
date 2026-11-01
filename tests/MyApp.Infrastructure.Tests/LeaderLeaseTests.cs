using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class LeaderLeaseTests : IDisposable
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
    private readonly SqliteTestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"myapp-lease-{Guid.CreateVersion7()}.db");

    private async Task<bool> TryAsync(string owner, string name = "job")
    {
        await using var db = _database.NewContext(_time);
        return await new LeaderLease(db, new UnitOfWork(db), _time).TryAcquireAsync(name, owner, Duration, CancellationToken.None);
    }

    [Fact]
    public async Task The_first_instance_gets_the_lease_and_a_second_one_waits()
    {
        (await TryAsync("a")).ShouldBeTrue();

        (await TryAsync("b")).ShouldBeFalse();
    }

    [Fact]
    public async Task The_holder_renews_before_it_expires_and_keeps_the_lease_past_the_first_expiry()
    {
        await TryAsync("a");
        _time.Advance(TimeSpan.FromSeconds(20));
        (await TryAsync("a")).ShouldBeTrue(); // renewed: now valid until 20 + 30 seconds

        _time.Advance(TimeSpan.FromSeconds(20)); // 40 seconds in: past the first expiry, inside the renewed one

        (await TryAsync("b")).ShouldBeFalse();
    }

    [Fact]
    public async Task When_the_holder_stops_another_instance_takes_over_after_the_lease_expires()
    {
        await TryAsync("a");
        _time.Advance(Duration + TimeSpan.FromSeconds(1));

        (await TryAsync("b")).ShouldBeTrue();
        (await TryAsync("a")).ShouldBeFalse(); // the old leader comes back and finds it is no longer the leader
    }

    [Fact]
    public async Task Leases_with_different_names_are_independent()
    {
        await TryAsync("a", "relay");

        (await TryAsync("b", "retention")).ShouldBeTrue();
    }

    [Fact]
    public async Task Instances_racing_for_the_same_lease_have_exactly_one_winner()
    {
        await using (var setup = NewFileContext())
        {
            await setup.Database.MigrateAsync();
        }

        for (var round = 0; round < 10; round++)
        {
            var name = $"race-{round}";
            var attempts = Enumerable.Range(0, 4).Select(async i =>
            {
                await using var db = NewFileContext();
                return await new LeaderLease(db, new UnitOfWork(db), TimeProvider.System).TryAcquireAsync(name, $"instance-{i}", Duration, CancellationToken.None);
            }).ToList();

            (await Task.WhenAll(attempts)).Count(won => won).ShouldBe(1, $"round {round}");
        }
    }

    private AppDbContext NewFileContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_file}").Options, TimeProvider.System);

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }
}
