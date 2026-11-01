using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;

namespace MyApp.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    // SQLITE_CONSTRAINT_PRIMARYKEY and SQLITE_CONSTRAINT_UNIQUE.
    private const int PrimaryKeyViolation = 1555;
    private const int UniqueViolation = 2067;

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return PersistenceErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: PrimaryKeyViolation or UniqueViolation })
        {
            return PersistenceErrors.DuplicateKey;
        }
    }
}
