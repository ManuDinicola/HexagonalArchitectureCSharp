using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class DatabaseHealthCheckTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task A_reachable_database_is_healthy()
    {
        await using var db = _database.NewContext();

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task An_unreachable_database_is_unhealthy_not_an_exception()
    {
        var missingFolder = Path.Combine(Path.GetTempPath(), Guid.CreateVersion7().ToString("N"), "no.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = missingFolder, Mode = SqliteOpenMode.ReadOnly }.ToString())
            .Options;
        await using var db = new AppDbContext(options, TimeProvider.System);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    public void Dispose() => _database.Dispose();
}
