using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class ExpireOfficialPostsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpireOfficialPostsJob> logger) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

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
                logger.LogError(ex, "ExpireOfficialPostsJob pass failed");
            }
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfficialPostRepository>();
        int expired = await repo.ExpirePostsAsync(ct);
        if (expired > 0)
            logger.LogInformation("ExpireOfficialPostsJob: expired {Count} post(s)", expired);
    }
}
