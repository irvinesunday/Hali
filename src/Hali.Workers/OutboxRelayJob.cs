using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class OutboxRelayJob(IServiceScopeFactory scopeFactory, ILogger<OutboxRelayJob> logger) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunRelayPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxRelayJob pass failed");
            }
        }
    }

    private async Task RunRelayPassAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var relay = scope.ServiceProvider.GetRequiredService<IOutboxRelayService>();
        var count = await relay.ProcessPendingAsync(ct);
        if (count > 0)
            logger.LogDebug("OutboxRelayJob: processed {Count} event(s)", count);
    }
}
