using MyApp.Application.Ports.Out;

namespace MyApp.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
