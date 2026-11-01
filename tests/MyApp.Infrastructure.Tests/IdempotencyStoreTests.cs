using MyApp.Application.Idempotency;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Tests;

public sealed class IdempotencyStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private static IdempotencyRecord Record(string scope = "PlaceOrderCommand", string key = "key-1") =>
        new(scope, key, new string('A', 64), "\"0f8fad5b-d9cb-469f-a165-70867728950e\"", DateTimeOffset.UnixEpoch);

    private async Task<Application.Ports.Out.IIdempotencyStore> SaveAsync(IdempotencyRecord record)
    {
        await using var db = _database.NewContext();
        new IdempotencyStore(db).Add(record);
        (await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess.ShouldBeTrue();
        return new IdempotencyStore(db);
    }

    [Fact]
    public async Task A_saved_record_is_found_by_scope_and_key()
    {
        await SaveAsync(Record());

        await using var db = _database.NewContext();
        var found = await new IdempotencyStore(db).FindAsync("PlaceOrderCommand", "key-1", CancellationToken.None);

        found.ShouldBe(Record());
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_scope_or_key_finds_nothing()
    {
        await SaveAsync(Record());

        await using var db = _database.NewContext();
        var store = new IdempotencyStore(db);

        (await store.FindAsync("CancelOrderCommand", "key-1", CancellationToken.None)).ShouldBeNull();
        (await store.FindAsync("PlaceOrderCommand", "key-2", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_first_calls_with_the_same_key_cannot_both_win()
    {
        await SaveAsync(Record());

        await using var db = _database.NewContext();
        new IdempotencyStore(db).Add(Record());
        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        result.Errors.ShouldHaveSingleItem().ShouldBe(PersistenceErrors.DuplicateKey);
    }

    public void Dispose() => _database.Dispose();
}
