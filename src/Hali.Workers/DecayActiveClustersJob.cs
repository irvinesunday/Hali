using Hali.Application.Clusters;
using Microsoft.Extensions.DependencyInjection;

namespace Hali.Workers;

/// <summary>
/// Runs every 5 minutes. Fetches all active and possible_restoration clusters
/// and applies CIVIS exponential decay, transitioning state when mass drops below threshold.
/// </summary>
public sealed class DecayActiveClustersJob(
    IServiceScopeFactory scopeFactory,
    ILogger<DecayActiveClustersJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunDecayPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "DecayActiveClustersJob pass failed");
            }
        }
    }

    private async Task RunDecayPassAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        var civis = scope.ServiceProvider.GetRequiredService<ICivisEvaluationService>();

        var clusters = await clusterRepo.GetActiveClustersForDecayAsync(ct);
        if (clusters.Count == 0)
            return;

        logger.LogInformation("DecayActiveClustersJob: evaluating {Count} cluster(s)", clusters.Count);

        foreach (var cluster in clusters)
        {
            await civis.ApplyDecayAsync(cluster.Id, ct);
        }
    }
}
