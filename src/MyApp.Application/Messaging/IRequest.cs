using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>A command or query. Always answers with a Result.</summary>
public interface IRequest<TResponse> where TResponse : IResult<TResponse>;
