using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

/// <summary>Real EF Core against in-memory SQLite. The connection must stay open or the database disappears.</summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Every call is a fresh context (empty change tracker) on the same database.</summary>
    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, TimeProvider.System);

    public void Dispose() => _connection.Dispose();
}
