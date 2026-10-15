using BMoni.Application.Bmoni;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BMoni.Infrastructure.Platform;

internal sealed class BmoniTransferReconciliationWorker(IServiceScopeFactory scopeFactory,
    IConfiguration configuration, ILogger<BmoniTransferReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (string.IsNullOrWhiteSpace(configuration["BMONI_API_KEY"])) continue;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IBmoniGateway>()
                    .ReconcilePendingTransfersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "BMONI transfer reconciliation failed."); }
        }
    }
}
