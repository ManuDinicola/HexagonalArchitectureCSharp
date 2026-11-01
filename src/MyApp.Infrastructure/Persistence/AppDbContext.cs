using Microsoft.EntityFrameworkCore;
using MyApp.Application.Idempotency;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Infrastructure.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider time) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RelayLease> RelayLeases => Set<RelayLease>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var raisedBy = StageOutboxMessages();
        var saved = base.SaveChanges(acceptAllChangesOnSuccess);
        ClearEvents(raisedBy);
        return saved;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var raisedBy = StageOutboxMessages();
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        ClearEvents(raisedBy);
        return saved;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    /// <summary>
    /// The outbox: every event raised by a tracked aggregate becomes a row, added to the same save as the aggregate itself.
    /// Either both are committed or neither is. Events are cleared only after the save succeeded. Also stamps the aggregates' versions.
    /// </summary>
    private List<IHasDomainEvents> StageOutboxMessages()
    {
        ChangeTracker.DetectChanges();
        var raisedBy = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.State != EntityState.Detached && e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var now = time.GetUtcNow();
        foreach (var domainEvent in raisedBy.SelectMany(a => a.DomainEvents))
        {
            OutboxMessages.Add(OutboxMessage.From(domainEvent, Guid.CreateVersion7(now), now));
        }

        ChangeTracker.StampVersions(Guid.CreateVersion7(now));
        return raisedBy;
    }

    private static void ClearEvents(List<IHasDomainEvents> raisedBy)
    {
        foreach (var aggregate in raisedBy)
        {
            aggregate.ClearDomainEvents();
        }
    }
}
