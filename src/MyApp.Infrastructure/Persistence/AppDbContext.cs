using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider time) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.StampVersions(Guid.CreateVersion7(time.GetUtcNow()));
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.StampVersions(Guid.CreateVersion7(time.GetUtcNow()));
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
