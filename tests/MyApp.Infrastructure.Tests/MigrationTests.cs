using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

/// <summary>The schema comes from the migrations, not from the model. These tests keep the two in step.</summary>
public sealed class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, TimeProvider.System);

    [Fact]
    public async Task The_migrations_build_a_database_the_app_can_use()
    {
        await _connection.OpenAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var order = Order.Place(Guid.CreateVersion7(DateTimeOffset.UnixEpoch), "cust-1", [new OrderLineDraft("SKU-1", 2, 5m)], DateTimeOffset.UnixEpoch).Unwrap();

        new OrderRepository(db).Add(order);
        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();

        await using var read = NewContext();
        (await new OrderRepository(read).GetByIdReadOnlyAsync(order.Id, CancellationToken.None)).ShouldNotBeNull().Total.ShouldBe(10m);
    }

    [Fact]
    public async Task Existing_text_timestamps_survive_the_move_to_utc_ticks()
    {
        await _connection.OpenAsync();
        await using var db = NewContext();
        var migrator = db.GetService<IMigrator>();
        var before = db.Database.GetMigrations().Single(m => m.EndsWith("_AddOutbox", StringComparison.Ordinal));
        await migrator.MigrateAsync(before); // the schema as it was: timestamps are ISO text

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO IdempotencyRecords (Scope, Key, Fingerprint, Response, CreatedAt, Version) VALUES ('S', 'K', 'F', 'R', '2026-03-04T05:06:07.1234567+00:00', '11111111-1111-1111-1111-111111111111')");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO OutboxMessages (MessageId, Type, Payload, OccurredAt, CreatedAt, ProcessedAt, Attempts, Version) VALUES ('22222222-2222-2222-2222-222222222222', 'T', 'P', '2026-03-04T05:06:07+00:00', '2026-03-04T05:06:07+00:00', '2026-03-04T05:06:08+00:00', 0, '11111111-1111-1111-1111-111111111111')");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO OutboxMessages (MessageId, Type, Payload, OccurredAt, CreatedAt, ProcessedAt, Attempts, Version) VALUES ('33333333-3333-3333-3333-333333333333', 'T', 'P', '2026-03-04T05:06:07+00:00', '2026-03-04T05:06:07+00:00', NULL, 0, '11111111-1111-1111-1111-111111111111')");

        await db.Database.MigrateAsync(); // to the latest

        var record = await db.IdempotencyRecords.AsNoTracking().SingleAsync();
        record.CreatedAt.ShouldBe(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)); // kept to the second
        var messages = await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync();
        messages[0].ProcessedAt.ShouldBe(new DateTimeOffset(2026, 3, 4, 5, 6, 8, TimeSpan.Zero));
        messages[1].ProcessedAt.ShouldBeNull();
    }

    [Fact]
    public async Task The_model_has_no_change_without_a_migration()
    {
        await _connection.OpenAsync();
        await using var db = NewContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse(
            "The EF model changed but no migration was added. See 'Migrations' in ARCHITECTURE.md.");
    }

    [Fact]
    public async Task Applying_the_migrations_twice_changes_nothing()
    {
        await _connection.OpenAsync();
        await using var db = NewContext();

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
    }

    public void Dispose() => _connection.Dispose();
}
