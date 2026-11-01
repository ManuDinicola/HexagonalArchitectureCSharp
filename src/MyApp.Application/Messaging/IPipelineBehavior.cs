using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>Cross-cutting step around every handler. Registration order = execution order (first = outermost).</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
