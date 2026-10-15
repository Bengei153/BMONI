using BMoni.Domain.Bmoni;
using BMoni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BMoni.Infrastructure.Platform;

internal sealed class BmoniWebhookWorker(IServiceScopeFactory scopeFactory,
    ILogger<BmoniWebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "BMONI webhook processing failed."); }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.BmoniWebhookEvents.Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.ReceivedAt).Take(50).ToListAsync(cancellationToken);
        if (events.Count == 0) return;

        foreach (var item in events)
        {
            if (item.EventType is "onboarding.completed" or "onboarding.failed" or "kyc.action_required")
            {
                var user = string.IsNullOrWhiteSpace(item.BmoniUserId) ? null :
                    await db.Users.SingleOrDefaultAsync(candidate => candidate.BmoniUserId == item.BmoniUserId, cancellationToken);
                if (user is not null)
                {
                    var status = item.EventType switch
                    {
                        "onboarding.completed" => "completed",
                        "onboarding.failed" => "failed",
                        _ => "kyc_action_required"
                    };
                    user.RecordBmoniOnboardingStatus(status);
                }
            }
            // Wallet events are recorded and deduplicated; platform lending balances remain simulated.
            item.MarkProcessed(DateTimeOffset.UtcNow);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
