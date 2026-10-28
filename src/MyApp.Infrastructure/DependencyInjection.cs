using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Application.Ports.Out;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure;

/// <summary>The ONLY public type of this assembly. Everything else is an internal adapter.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System); // overridable, like in AddApplication()
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
