using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

/// <summary>Saves a command's staged changes in one transaction, and only when the handler returned a success.</summary>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (response.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
