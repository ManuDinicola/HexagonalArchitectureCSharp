using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Application.Behaviors;
using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System); // overridable: register your own TimeProvider first
        services.AddScoped<IDispatcher, Dispatcher>();

        // Order matters: first registered = outermost. Telemetry wraps everything; Logging sees validation failures.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>)); // commands only (constraint)

        // Handlers and validators are found by scanning; nobody registers them by hand.
        var types = typeof(DependencyInjection).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }).ToList();
        foreach (var type in types)
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (definition == typeof(IRequestHandler<,>) || definition == typeof(IValidator<>))
                {
                    services.AddScoped(contract, type);
                }
            }
        }

        // Idempotent commands get their behavior after the open ones above, so it runs innermost: inside the unit of work.
        foreach (var type in types)
        {
            foreach (var value in type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>))
                .Select(i => i.GetGenericArguments()[0]))
            {
                services.AddScoped(
                    typeof(IPipelineBehavior<,>).MakeGenericType(type, typeof(Result<>).MakeGenericType(value)),
                    typeof(IdempotencyBehavior<,>).MakeGenericType(type, value));
            }
        }

        return services;
    }
}
