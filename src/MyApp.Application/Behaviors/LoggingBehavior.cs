using Microsoft.Extensions.Logging;
using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", name);

        try
        {
            var response = await next();

            if (response.IsSuccess)
                logger.LogInformation("Handled {Request}", name);
            else
                logger.LogWarning("{Request} failed: {Errors}", name, string.Join(", ", response.Errors.Select(e => e.Code)));

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Request} threw an unexpected exception", name);
            throw;
        }
    }
}
