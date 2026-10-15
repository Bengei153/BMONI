using BMoni.Application.Bmoni;
using BMoni.Application.Loans;
using BMoni.Domain.Loans;
using BMoni.Domain.Users;
using BMoni.Infrastructure.Persistence;
using BMoni.Infrastructure.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BMoni.Infrastructure.Tests;

public sealed class LoanJourneyTests
{
    [Fact]
    public async Task Borrower_and_two_lenders_complete_approved_loan_and_return_exact_interest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DEMO_USER_PASSWORD"] = "HackathonDemoPass123",
            ["BMONI_BASE_URL"] = "https://embedded-dev.bmoni.com"
        }).Build();
        var advisory = new FixedAdvisoryService();
        var service = new PlatformService(db, configuration, advisory, new NoOpBmoniGateway());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = cancellation.Token;

        var bootstrap = await service.BootstrapAsync(token);
        Assert.True(bootstrap.Ready);
        var secondBootstrap = await service.BootstrapAsync(token);
        Assert.True(secondBootstrap.Ready);
        Assert.Equal(3, await db.Users.CountAsync(token));

        var tolu = bootstrap.DemoAccounts.Single(account => account.Email == "tolu@bmoni.demo");
        var ada = bootstrap.DemoAccounts.Single(account => account.Email == "ada@bmoni.demo");
        var kunle = bootstrap.DemoAccounts.Single(account => account.Email == "kunle@bmoni.demo");
        Assert.Equal(20_000m, (await service.GetWalletAsync(ada.Id, token)).Balance);

        await service.DepositAsync(tolu.Id, 500m, "journey-topup", token);
        var request = new CreateLoanRequest(5_000m, "Course materials",
            "I need course materials and will repay from my stipend.", 30, 4);
        var loan = await service.CreateLoanAsync(tolu.Id, request, token);
        Assert.Equal(50, loan.Score);
        Assert.False(loan.AiAvailable);

        var partiallyFunded = await service.ContributeAsync(ada.Id, loan.Id, 2_500m, true, "journey-ada", token);
        Assert.Equal(2_500m, partiallyFunded.Funded);
        var fullyFunded = await service.ContributeAsync(kunle.Id, loan.Id, 2_500m, true, "journey-kunle", token);
        Assert.Equal("AwaitingBorrowerAcceptance", fullyFunded.Status);
        Assert.Equal(5_000m, fullyFunded.Funded);

        var active = await service.AcceptTermsAsync(tolu.Id, loan.Id, "journey-accept", token);
        Assert.Equal("Active", active.Status);
        Assert.Equal(4, active.Schedule.Count);

        var spend = await service.RequestWalletSpendAsync(tolu.Id, 50m, "Course materials", "journey-spend", token);
        Assert.False(spend.Confirmed);
        var spendResult = await service.ConfirmApprovalAsync(tolu.Id, spend.Id, "journey-spend-confirm", token);
        Assert.Equal(5_450m, spendResult.Wallet!.Balance);
        var spendReplay = await service.ConfirmApprovalAsync(tolu.Id, spend.Id, "journey-spend-confirm", token);
        Assert.Equal(5_450m, spendReplay.Wallet!.Balance);

        var partial = await service.RequestRepaymentAsync(tolu.Id, loan.Id, 600m, "journey-repay-partial", token);
        Assert.False(partial.Confirmed);
        var partialResult = await service.ConfirmApprovalAsync(tolu.Id, partial.Id, "journey-repay-partial-confirm", token);
        Assert.Equal("Active", partialResult.Loan!.Status);

        var final = await service.RequestRepaymentAsync(tolu.Id, loan.Id, 4_800m, "journey-repay-final", token);
        var finalResult = await service.ConfirmApprovalAsync(tolu.Id, final.Id, "journey-repay-final-confirm", token);
        Assert.Equal("Repaid", finalResult.Loan!.Status);
        Assert.Equal(5_400m, finalResult.Loan.Schedule.Sum(installment => installment.Amount));
        var finalReplay = await service.ConfirmApprovalAsync(tolu.Id, final.Id, "journey-repay-final-confirm", token);
        Assert.Equal("Repaid", finalReplay.Loan!.Status);

        var adaReturns = await service.GetLenderReturnsAsync(ada.Id, token);
        var kunleReturns = await service.GetLenderReturnsAsync(kunle.Id, token);
        Assert.Equal(2_500m, adaReturns.PrincipalCommitted);
        Assert.Equal(2_500m, adaReturns.PrincipalReturned);
        Assert.Equal(200m, adaReturns.InterestEarned);
        Assert.Equal(0m, adaReturns.OutstandingExposure);
        Assert.Equal(2_500m, kunleReturns.PrincipalReturned);
        Assert.Equal(200m, kunleReturns.InterestEarned);

        Assert.Equal(13, await db.LedgerEntries.CountAsync(token));
        Assert.All(await db.Approvals.ToListAsync(token), approval => Assert.True(approval.Confirmed));
        var dashboard = await service.GetDashboardAsync(tolu.Id, token);
        Assert.Equal(50m, dashboard.Wallet.Balance);
        Assert.Equal(29m, dashboard.RunwayDays);
    }

    private sealed class FixedAdvisoryService : IAdvisoryService
    {
        public Task<ScamCheckResponse> CheckScamAsync(Guid userId, ScamCheckRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AssistantChatResponse> ChatAsync(DashboardDto facts, string question, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StatementAssessmentResult> AssessStatementAsync(StatementAssessmentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new StatementAssessmentResult(50, 50, 50, ["assessment unavailable"], [], false));
    }

    private sealed class NoOpBmoniGateway : IBmoniGateway
    {
        public Task<BmoniProvisionedAccount> ProvisionNigeriaAccountAsync(Guid localUserId, BmoniUserProfile profile,
            BmoniKycSubmission kyc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement> GetBalancesAsync(Guid localUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<System.Text.Json.JsonElement> GetTransactionsAsync(Guid localUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BmoniAccountVerification> VerifyNigerianAccountAsync(Guid localUserId, string bankCode,
            string accountNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BmoniTransferResult> TransferAsync(Guid localUserId, string toBmoniUserId, decimal amount,
            string description, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReconcilePendingTransfersAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
