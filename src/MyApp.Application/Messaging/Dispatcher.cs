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

        var invoker = (IInvoker<TResponse>)Invokers.GetOrAdd(request.GetType(), static (requestType, _) =>
            Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(requestType, typeof(TResponse)))!, 0);

        return invoker.InvokeAsync(request, services, cancellationToken);
    }

    private interface IInvoker<TResponse> where TResponse : IResult<TResponse>
    {
        Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class Invoker<TRequest, TResponse> : IInvoker<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResult<TResponse>
    {
        public Task<TResponse> InvokeAsync(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
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
