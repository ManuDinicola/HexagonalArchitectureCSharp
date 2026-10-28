using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();
        foreach (var validator in validators)
        {
            errors.AddRange(await validator.ValidateAsync(request, cancellationToken));
        }

        return errors.Count > 0 ? TResponse.FromErrors(errors) : await next();
    }
}
