using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Application.Tests.Fakes;

namespace MyApp.Application.Tests;

/// <summary>The real DI + real pipeline; only the outbound ports are fakes.</summary>
public sealed class TestApp : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public TestApp()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddApplication();
        services.AddSingleton(Orders);
        services.AddSingleton<IOrderRepository>(Orders);
        services.AddSingleton<IClock>(Clock);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public ListLoggerProvider Logs { get; } = new();
    public FakeOrderRepository Orders { get; } = new();
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero));

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        where TResponse : Domain.Common.IResult<TResponse>
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(request);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
