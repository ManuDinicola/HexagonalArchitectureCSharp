using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyApp.Application.Ports.Out;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure;

/// <summary>The ONLY public type of this assembly. Everything else is an internal adapter.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the adapters (EF Core + SQLite, outbox, background jobs) behind the Application ports.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="migrateOnStartup">Apply pending migrations when the host starts. Set to false and migrate as a deploy step when you run several instances.</param>
    /// <param name="runBackgroundJobs">Run the outbox relay and the retention purge in this process. Safe on every instance: they take turns through a lease.</param>
    /// <param name="outboxPollInterval">How often the relay looks for new messages (default 2 seconds).</param>
    /// <param name="retention">How long idempotency records and delivered outbox messages are kept (default 7 days). Must be longer than your clients' retry window.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool migrateOnStartup = true,
        bool runBackgroundJobs = true,
        TimeSpan? outboxPollInterval = null,
        TimeSpan? retention = null)
    {
        var poll = outboxPollInterval ?? TimeSpan.FromSeconds(2);
        var keep = retention ?? TimeSpan.FromDays(7);
        var lease = poll * 15 > TimeSpan.FromSeconds(30) ? poll * 15 : TimeSpan.FromSeconds(30); // renewed every poll: it only expires when the holder is gone

        services.TryAddSingleton(TimeProvider.System); // overridable, like in AddApplication()
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReadStore, OrderReadStore>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddSingleton<IMessagePublisher, LoggingMessagePublisher>(); // overridable: register your broker adapter first
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<LeaderLease>();
        services.AddScoped(sp => new RetentionPurger(
            sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<TimeProvider>(), keep, sp.GetRequiredService<ILogger<RetentionPurger>>()));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        // Registered before the background jobs on purpose: hosted services start in registration order, and the jobs need the schema.
        // Fine for a template and for one instance. With several instances or a strict deploy process, set migrateOnStartup to false and apply the migrations as a deploy step.
        if (migrateOnStartup)
        {
            services.AddHostedService<DatabaseInitializer>();
        }

        // Every instance may run these: the relay and the purge each hold a lease in the database, so only one instance works at a time and the others stand by.
        if (runBackgroundJobs)
        {
            services.AddHostedService(sp => new OutboxRelay(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<TimeProvider>(),
                poll,
                lease,
                NewInstanceId(sp),
                sp.GetRequiredService<ILogger<OutboxRelay>>()));
            services.AddHostedService(sp => new RetentionWorker(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<TimeProvider>(),
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5),
                NewInstanceId(sp),
                sp.GetRequiredService<ILogger<RetentionWorker>>()));
        }

        return services;
    }

    private static string NewInstanceId(IServiceProvider services) =>
        Guid.CreateVersion7(services.GetRequiredService<TimeProvider>().GetUtcNow()).ToString("N");
}
