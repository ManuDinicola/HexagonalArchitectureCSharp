using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>A request that changes state. Its changes are saved once, and only if the handler succeeds (see UnitOfWorkBehavior).</summary>
public interface ICommand<TResponse> : IRequest<TResponse> where TResponse : IResult<TResponse>;
