using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : IResult<TResponse>;
}
