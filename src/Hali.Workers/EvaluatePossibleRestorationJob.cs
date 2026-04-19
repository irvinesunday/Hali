using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class EvaluatePossibleRestorationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EvaluatePossibleRestorationJob> logger) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "EvaluatePossibleRestorationJob pass failed");
            }
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        logger.LogInformation("{job} {event}", "EvaluatePossibleRestorationJob", "start");
        await using var scope = scopeFactory.CreateAsyncScope();
        var clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        var restorationService = scope.ServiceProvider.GetRequiredService<IRestorationEvaluationService>();

        var clusters = await clusterRepo.GetPossibleRestorationClustersAsync(ct);
        if (clusters.Count == 0)
        {
            logger.LogInformation("{job} {event} clustersProcessed=0", "EvaluatePossibleRestorationJob", "complete");
            return;
        }

        logger.LogInformation("EvaluatePossibleRestorationJob: evaluating {Count} cluster(s)", clusters.Count);

        int processed = 0;
        foreach (SignalCluster cluster in clusters)
        {
            await restorationService.EvaluateAsync(cluster.Id, ct);
            processed++;
        }
        logger.LogInformation("{job} {event} clustersProcessed={Count}", "EvaluatePossibleRestorationJob", "complete", processed);
    }
}
