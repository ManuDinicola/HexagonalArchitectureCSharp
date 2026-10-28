using Microsoft.Extensions.Logging;
using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

internal sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        LogHandling(name);

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            LogCrashed(ex, name);
            throw;
        }

        if (response.IsSuccess)
        {
            LogHandled(name);
        }
        else
        {
            LogFailed(name, string.Join(", ", response.Errors.Select(e => e.Code)));
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {Request}")]
    private partial void LogHandling(string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {Request}")]
    private partial void LogHandled(string request);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Request} threw an unhandled exception")]
    private partial void LogCrashed(Exception exception, string request);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Request} failed: {Errors}")]
    private partial void LogFailed(string request, string errors);
}
