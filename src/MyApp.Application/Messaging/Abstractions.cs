using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>A command or query. Always answers with a Result.</summary>
public interface IRequest<TResponse> where TResponse : IResult<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IResult<TResponse>;
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Cross-cutting step around every handler. Registration order = execution order (first = outermost).</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>Cheap checks on the shape of a request. Business invariants live in the Domain.</summary>
public interface IValidator<in TRequest>
{
    Task<IReadOnlyList<Error>> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}
