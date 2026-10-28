using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IResult<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (Invoker<TResponse>)Invokers.GetOrAdd(request.GetType(), static (requestType, _) =>
            Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(requestType, typeof(TResponse)))!, 0);

        return invoker.InvokeAsync(request, services, cancellationToken);
    }

    private abstract class Invoker<TResponse> where TResponse : IResult<TResponse>
    {
        public abstract Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class Invoker<TRequest, TResponse> : Invoker<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResult<TResponse>
    {
        public override Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
        {
            var typedRequest = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(typedRequest, ct);
            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = pipeline;
                pipeline = () => behavior.HandleAsync(typedRequest, next, ct);
            }

            return pipeline();
        }
    }
}
