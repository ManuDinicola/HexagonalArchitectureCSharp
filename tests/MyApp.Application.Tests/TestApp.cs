using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Application.Tests.Fakes;
using MyApp.Domain.Common;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Application.Tests;

/// <summary>The real DI + real pipeline; only the outbound ports are FakeItEasy fakes.</summary>
public sealed class TestApp : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public TestApp()
    {
        // Behaves like a tiny in-memory repository.
        // Staged until SaveChangesAsync, like a real unit of work.
        A.CallTo(() => Orders.Add(A<Order>._)).Invokes((Order o) => Staged.Add(o));
        A.CallTo(() => UnitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                Saved.AddRange(Staged);
                Staged.Clear();
                return Task.FromResult(Result.Success());
            });
        A.CallTo(() => Orders.GetByIdReadOnlyAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Saved.FirstOrDefault(o => o.Id == id)));
        A.CallTo(() => Time.GetUtcNow()).Returns(Now);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddApplication();
        services.AddSingleton(Orders);
        services.AddSingleton(UnitOfWork);
        services.AddSingleton(Time);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public static DateTimeOffset Now { get; } = new(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);

    public ListLoggerProvider Logs { get; } = new();
    public IOrderRepository Orders { get; } = A.Fake<IOrderRepository>();
    public IUnitOfWork UnitOfWork { get; } = A.Fake<IUnitOfWork>();
    public TimeProvider Time { get; } = A.Fake<TimeProvider>();
    public List<Order> Staged { get; } = [];
    public List<Order> Saved { get; } = [];

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        where TResponse : IResult<TResponse>
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(request);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
