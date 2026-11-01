using MyApp.Domain.Common;

namespace MyApp.Application.Ports.Out;

/// <summary>Commits everything staged by repositories in one transaction. Only the pipeline calls it, never a use case.</summary>
public interface IUnitOfWork
{
    /// <summary>Expected storage failures (duplicate key, concurrent update) come back as a <c>Conflict</c> error.</summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
