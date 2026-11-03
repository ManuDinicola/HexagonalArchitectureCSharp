using System.Diagnostics;
using System.Diagnostics.Metrics;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;

namespace MyApp.Application.Tests;

/// <summary>Listens like OpenTelemetry would. The source and meter are process-wide, so each test looks only at its own request type.</summary>
public sealed class TelemetryTests : IAsyncDisposable
{
    private readonly TestApp _app = new();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static ActivityListener ListenToSpans(List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, Telemetry.Name, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (stopped)
                {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task A_successful_use_case_is_one_span_named_after_the_request()
    {
        var stopped = new List<Activity>();
        using var listener = ListenToSpans(stopped);

        await _app.SendAsync(new PlaceOrderCommand("cust-1", [new PlaceOrderLine("SKU-1", 1, 1m)]));

        Activity span;
        lock (stopped)
        {
            span = stopped.Where(a => string.Equals(a.OperationName, nameof(PlaceOrderCommand), StringComparison.Ordinal)).ShouldHaveSingleItem();
        }

        span.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task A_failed_use_case_marks_its_span_as_an_error_with_the_error_code()
    {
        var stopped = new List<Activity>();
        using var listener = ListenToSpans(stopped);

        await _app.SendAsync(new GetOrderQuery(Guid.NewGuid()));

        Activity span;
        lock (stopped)
        {
            span = stopped.Where(a => string.Equals(a.OperationName, nameof(GetOrderQuery), StringComparison.Ordinal)).ShouldHaveSingleItem();
        }

        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe("Order.NotFound");
    }

    [Fact]
    public async Task Every_use_case_is_counted_by_request_and_outcome_and_timed()
    {
        var counts = new List<(string Request, string Outcome, long Value)>();
        var durations = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (string.Equals(instrument.Meter.Name, Telemetry.Name, StringComparison.Ordinal))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            lock (counts)
            {
                counts.Add((Tag(tags, "request"), Tag(tags, "outcome"), value));
            }
        });
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            lock (durations)
            {
                durations.Add(Tag(tags, "request"));
            }
        });
        listener.Start();

        await _app.SendAsync(new GetOrderQuery(Guid.NewGuid()));

        lock (counts)
        {
            counts.ShouldContain((nameof(GetOrderQuery), "failure", 1L));
        }

        lock (durations)
        {
            durations.Contains(nameof(GetOrderQuery), StringComparer.Ordinal).ShouldBeTrue();
        }
    }

    private static string Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (string.Equals(tag.Key, name, StringComparison.Ordinal))
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
