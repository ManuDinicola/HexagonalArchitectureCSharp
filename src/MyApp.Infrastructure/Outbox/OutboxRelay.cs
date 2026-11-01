using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// Background loop around <see cref="OutboxProcessor"/>. With several instances only the one holding the "outbox-relay" lease publishes, so the order of
/// messages is kept across the whole system; the others keep polling the lease and take over within <c>leaseDuration</c> if the leader stops.
/// A failing batch never stops the loop.
/// </summary>
internal sealed partial class OutboxRelay(
    IServiceScopeFactory scopes,
    TimeProvider time,
    TimeSpan pollInterval,
    TimeSpan leaseDuration,
    string instanceId,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    public const string LeaseName = "outbox-relay";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wasLeader = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var isLeader = await scope.ServiceProvider.GetRequiredService<LeaderLease>()
                    .TryAcquireAsync(LeaseName, instanceId, leaseDuration, stoppingToken);

                if (isLeader != wasLeader)
                {
                    LogLeadership(instanceId, isLeader);
                    wasLeader = isLeader;
                }

                if (isLeader)
                {
                    published = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogBatchFailed(ex);
            }

            if (published < OutboxProcessor.BatchSize)
            {
                try
                {
                    await Task.Delay(pollInterval, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {InstanceId}: leader = {IsLeader}")]
    private partial void LogLeadership(string instanceId, bool isLeader);

    [LoggerMessage(Level = LogLevel.Error, Message = "The outbox relay failed to process a batch; it will try again")]
    private partial void LogBatchFailed(Exception exception);
}
