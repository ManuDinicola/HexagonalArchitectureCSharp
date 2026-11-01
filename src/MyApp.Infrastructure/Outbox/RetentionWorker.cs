using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Outbox;

/// <summary>
/// Runs <see cref="RetentionPurger"/> on a schedule. Under a lease like the relay's, so several instances do not all delete at once
/// (they could, deleting is idempotent, but there is no reason to). A failed purge is logged and retried at the next interval.
/// </summary>
internal sealed partial class RetentionWorker(
    IServiceScopeFactory scopes,
    TimeProvider time,
    TimeSpan interval,
    TimeSpan leaseDuration,
    string instanceId,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    public const string LeaseName = "retention";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<LeaderLease>().TryAcquireAsync(LeaseName, instanceId, leaseDuration, stoppingToken))
                {
                    await scope.ServiceProvider.GetRequiredService<RetentionPurger>().PurgeAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogPurgeFailed(ex);
            }

            try
            {
                await Task.Delay(interval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The retention purge failed; it will run again at the next interval")]
    private partial void LogPurgeFailed(Exception exception);
}
