using System.Diagnostics;
using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.Behaviors;

/// <summary>Outermost step: one span and one count and duration measurement per use case. Logs written inside carry the span's trace id.</summary>
internal sealed class TelemetryBehavior<TRequest, TResponse>(TimeProvider time)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        using var activity = ApplicationTelemetry.Source.StartActivity(name);
        var start = time.GetTimestamp();

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            Record(name, "error", start);
            throw;
        }

        if (!response.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, response.Errors[0].Code);
        }

        Record(name, response.IsSuccess ? "success" : "failure", start);
        return response;
    }

    private void Record(string request, string outcome, long start)
    {
        var tags = new TagList { { "request", request }, { "outcome", outcome } };
        ApplicationTelemetry.Requests.Add(1, tags);
        ApplicationTelemetry.Duration.Record(time.GetElapsedTime(start).TotalMilliseconds, tags);
    }
}
