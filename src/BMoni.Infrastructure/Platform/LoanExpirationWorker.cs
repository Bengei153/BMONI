using BMoni.Domain.Loans;
using BMoni.Domain.Wallets;
using BMoni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BMoni.Infrastructure.Platform;

internal sealed class LoanExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<LoanExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireLoansAsync(stoppingToken);
                await MarkOverdueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Loan expiration sweep failed."); }
        }
    }

    private async Task ExpireLoansAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        // SQLite cannot translate comparisons against this DateTimeOffset value.
        // There are few open loans in the demo, so filter the candidate set in memory;
        // PostgreSQL uses the same provider-neutral path.
        var expired = (await db.Loans.Where(loan => loan.Status == LoanStatus.OpenForFunding)
            .ToListAsync(cancellationToken)).Where(loan => loan.FundingDeadline < now).ToList();
        if (expired.Count == 0) return;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var loan in expired)
        {
            if (!loan.ExpireFunding(now)) continue;
            var contributions = await db.LoanContributions.Where(item => item.LoanId == loan.Id && item.Status == ContributionStatus.Reserved)
                .ToListAsync(cancellationToken);
            foreach (var contribution in contributions)
            {
                contribution.Release();
                var wallet = await db.SimulatedWallets.SingleAsync(item => item.UserId == contribution.LenderId, cancellationToken);
                wallet.Credit(contribution.Amount);
                var key = $"loan-expiry-release:{contribution.Id:N}";
                if (!await db.LedgerEntries.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
                    db.LedgerEntries.Add(new LedgerEntry(contribution.LenderId, contribution.Amount,
                        "LoanContributionReleased", contribution.Id, key, now));
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkOverdueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var loans = await db.Loans.Where(loan => loan.Status == LoanStatus.Active).ToListAsync(cancellationToken);
        if (loans.Count == 0) return;
        var loanIds = loans.Select(loan => loan.Id).ToArray();
        var installments = await db.LoanInstallments.Where(item => loanIds.Contains(item.LoanId)).ToListAsync(cancellationToken);
        foreach (var installment in installments) installment.RefreshStatus(now);
        foreach (var loan in loans)
            if (installments.Any(item => item.LoanId == loan.Id && item.Status == InstallmentStatus.Defaulted))
                loan.MarkDefaulted();
        await db.SaveChangesAsync(cancellationToken);
    }
}
