using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class DecayActiveClustersJob(IServiceScopeFactory scopeFactory, ILogger<DecayActiveClustersJob> logger) : BackgroundService()
{
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5L);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using PeriodicTimer timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await RunDecayPassAsync(stoppingToken);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				logger.LogError(ex, "DecayActiveClustersJob pass failed");
			}
		}
	}

	private async Task RunDecayPassAsync(CancellationToken ct)
	{
		await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
		IClusterRepository clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
		ICivisEvaluationService civis = scope.ServiceProvider.GetRequiredService<ICivisEvaluationService>();
		IReadOnlyList<SignalCluster> clusters = await clusterRepo.GetActiveClustersForDecayAsync(ct);
		if (clusters.Count == 0)
		{
			return;
		}
		logger.LogInformation("DecayActiveClustersJob: evaluating {Count} cluster(s)", clusters.Count);
		foreach (SignalCluster cluster in clusters)
		{
			await civis.ApplyDecayAsync(cluster.Id, ct);
		}
	}
}
