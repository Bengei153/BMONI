using BMoni.Application.Loans;
using BMoni.Application.Bmoni;
using BMoni.Domain.Loans;
using BMoni.Domain.Users;
using BMoni.Domain.Wallets;
using BMoni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BMoni.Infrastructure.Platform;

internal sealed class PlatformService(AppDbContext db, IConfiguration configuration, IAdvisoryService advisory,
    IBmoniGateway bmoni) : IPlatformService
{
    public async Task<BootstrapResponse> BootstrapAsync(CancellationToken cancellationToken)
    {
        var password = configuration["DEMO_USER_PASSWORD"];
        var demoAccounts = new List<DemoAccountDto>();
        if (!string.IsNullOrWhiteSpace(password) && password.Length >= 12)
        {
            var specs = new[]
            {
                ("tolu@bmoni.demo", "Tolu", UserRole.Borrower, 0m),
                ("ada@bmoni.demo", "Ada", UserRole.Lender, 20_000m),
                ("kunle@bmoni.demo", "Kunle", UserRole.Lender, 15_000m)
            };
            foreach (var (email, name, roles, seedBalance) in specs)
            {
                var user = await db.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
                if (user is null)
                {
                    user = User.Register(email, name, BCrypt.Net.BCrypt.HashPassword(password), roles, DateTimeOffset.UtcNow);
                    db.Users.Add(user);
                }
                var wallet = await db.SimulatedWallets.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
                if (wallet is null)
                {
                    wallet = SimulatedWallet.Create(user.Id);
                    db.SimulatedWallets.Add(wallet);
                }
                var seedKey = $"demo-bootstrap:{user.Id:N}";
                var existingCredit = await db.LedgerEntries.AnyAsync(entry => entry.IdempotencyKey == seedKey, cancellationToken);
                if (seedBalance > 0 && !existingCredit)
                {
                    wallet.Credit(seedBalance);
                    db.LedgerEntries.Add(new LedgerEntry(user.Id, seedBalance, "DemoOpeningBalance", user.Id, seedKey, DateTimeOffset.UtcNow));
                }
                demoAccounts.Add(new DemoAccountDto(user.Id, email, password, name, roles));
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        var bmoniBaseUrl = configuration["BMONI_BASE_URL"] ?? "https://embedded-dev.bmoni.com";
        return new BootstrapResponse(demoAccounts.Count == 3, bmoniBaseUrl.Contains("embedded-dev", StringComparison.OrdinalIgnoreCase),
            UsesSimulatedFunds: true, BmoniConfigured: !string.IsNullOrWhiteSpace(configuration["BMONI_API_KEY"]),
            ClaudeAvailable: !string.IsNullOrWhiteSpace(configuration["ANTHROPIC_API_KEY"]), demoAccounts,
            new
            {
                tiers = new[]
                {
                    new { minimumScore = 80, rate = 0.03m, maximumAmount = 20_000m, declined = false },
                    new { minimumScore = 65, rate = 0.05m, maximumAmount = 10_000m, declined = false },
                    new { minimumScore = 50, rate = 0.08m, maximumAmount = 5_000m, declined = false },
                    new { minimumScore = 0, rate = 0m, maximumAmount = 0m, declined = true }
                },
                fundingDays = 7,
                installmentsMin = 1,
                installmentsMax = 4,
                paymentsRequireApproval = true
            },
            "Sandbox demo only. All platform ledger balances and loan movements are simulated; a score is an assessment, not a guarantee.");
    }

    public async Task<WalletDto> GetWalletAsync(Guid userId, CancellationToken cancellationToken) =>
        ToWallet(await GetWalletEntityAsync(userId, cancellationToken));

    public async Task<WalletDto> DepositAsync(Guid userId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var existing = await db.LedgerEntries.SingleOrDefaultAsync(entry => entry.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.Amount != amount || existing.Type != "SandboxDeposit")
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return ToWallet(await GetWalletEntityAsync(userId, cancellationToken));
        }
        var existingApproval = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingApproval is not null)
            throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        wallet.Credit(amount);
        var now = DateTimeOffset.UtcNow;
        var approval = new Approval(null, userId, ApprovalType.WalletDeposit, amount, idempotencyKey, now);
        approval.Confirm(idempotencyKey, now);
        db.Approvals.Add(approval);
        db.LedgerEntries.Add(new LedgerEntry(userId, amount, "SandboxDeposit", approval.Id, idempotencyKey, now));
        await db.SaveChangesAsync(cancellationToken);
        return ToWallet(wallet);
    }

    public async Task<ApprovalDto> RequestWalletSpendAsync(Guid userId, decimal amount, string description,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500) throw new ArgumentException("Description must be 1-500 characters.");
        var existing = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.Type != ApprovalType.WalletSpend || existing.Amount != amount || existing.Details != description)
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return ToApproval(existing);
        }
        _ = await GetWalletEntityAsync(userId, cancellationToken);
        var approval = new Approval(null, userId, ApprovalType.WalletSpend, amount, idempotencyKey, DateTimeOffset.UtcNow, description.Trim());
        db.Approvals.Add(approval);
        await db.SaveChangesAsync(cancellationToken);
        return ToApproval(approval);
    }

    public async Task<ApprovalDto> RequestBmoniTransferAsync(Guid userId, string toBmoniUserId, decimal amount,
        string description, string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(toBmoniUserId) || toBmoniUserId.Length > 128) throw new ArgumentException("A BMONI recipient user id is required.");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500) throw new ArgumentException("Description must be 1-500 characters.");
        var existing = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        var details = System.Text.Json.JsonSerializer.Serialize(new BmoniTransferDetails(toBmoniUserId, description.Trim()));
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.Type != ApprovalType.BmoniTransfer || existing.Amount != amount || existing.Details != details)
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return ToApproval(existing);
        }
        var user = await FindUserAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(user.BmoniUserId) || string.IsNullOrWhiteSpace(user.BmoniSmartWalletId))
            throw new InvalidOperationException("Complete BMONI sandbox onboarding before requesting a transfer.");
        var approval = new Approval(null, userId, ApprovalType.BmoniTransfer, amount, idempotencyKey, DateTimeOffset.UtcNow, details);
        db.Approvals.Add(approval);
        await db.SaveChangesAsync(cancellationToken);
        return ToApproval(approval);
    }

    public async Task<LoanDto> CreateLoanAsync(Guid userId, CreateLoanRequest request, CancellationToken cancellationToken)
    {
        ValidateCreateLoanRequest(request);
        var user = await FindUserAsync(userId, cancellationToken);
        if (!user.HasRole(UserRole.Borrower)) throw new UnauthorizedAccessException("Borrower role is required.");
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var deposits = await db.LedgerEntries.AsNoTracking().Where(item => item.UserId == userId && item.Type == "SandboxDeposit")
            .ToListAsync(cancellationToken);
        var recentDeposits = deposits.Where(item => item.CreatedAt >= now.AddDays(-90)).ToArray();
        var depositWeeks = recentDeposits.Select(item => item.CreatedAt.UtcDateTime.Date.AddDays(-(int)item.CreatedAt.UtcDateTime.DayOfWeek))
            .Distinct().Count();
        var completedRepayments = await db.Loans.CountAsync(loan => loan.BorrowerId == userId && loan.Status == LoanStatus.Repaid, cancellationToken);
        var defaults = await db.Loans.CountAsync(loan => loan.BorrowerId == userId && loan.Status == LoanStatus.Defaulted, cancellationToken);
        var highRiskChecks = (await db.ScamChecks.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken))
            .Count(item => item.RiskLevel == "high" && item.CreatedAt >= now.AddDays(-30));
        var bufferPoints = wallet.Balance >= request.Amount ? 10 : wallet.Balance >= request.Amount / 2 ? 5 : 0;
        var recurringDepositPoints = depositWeeks >= 3 ? 10 : 0;
        var repaymentPoints = Math.Min(15, completedRepayments * 5);
        var defaultPenalty = Math.Min(30, defaults * 15);
        var riskFlagPenalty = Math.Min(15, highRiskChecks * 5);
        var walletSignals = Math.Clamp(50 + bufferPoints + recurringDepositPoints + repaymentPoints - defaultPenalty - riskFlagPenalty, 0, 100);
        var dailyDeposits = recentDeposits.Sum(item => Math.Max(item.Amount, 0m)) / 90m;
        var statementAssessment = await advisory.AssessStatementAsync(new StatementAssessmentRequest(request.Purpose,
            request.Statement, request.Amount, request.RepaymentDays, request.Installments, dailyDeposits), cancellationToken);
        var aiScore = (statementAssessment.PurposeClarity + statementAssessment.AmountRealism + statementAssessment.RepaymentPlanFit) / 3m;
        var score = (int)decimal.Round(walletSignals * 0.8m + aiScore * 0.2m, 0, MidpointRounding.AwayFromZero);
        var loan = Loan.Request(userId, request.Amount, request.Purpose, request.Statement,
            request.RepaymentDays, request.Installments, score, now);
        var assessment = new CreditAssessment(loan.Id, userId, walletSignals, statementAssessment.PurposeClarity,
            statementAssessment.AmountRealism, statementAssessment.RepaymentPlanFit, score, statementAssessment.AiAvailable, now);
        db.Loans.Add(loan);
        db.CreditAssessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        return ToLoan(loan, assessment);
    }

    public async Task<IReadOnlyList<LoanDto>> GetLoansAsync(Guid userId, string? role, CancellationToken cancellationToken)
    {
        var loans = (await db.Loans.AsNoTracking().ToListAsync(cancellationToken)).OrderByDescending(loan => loan.CreatedAt).ToList();
        var contributions = await db.LoanContributions.AsNoTracking().Where(item => item.LenderId == userId)
            .Select(item => item.LoanId).Distinct().ToListAsync(cancellationToken);
        var results = loans.Where(loan => role?.Equals("open", StringComparison.OrdinalIgnoreCase) == true
                ? loan.Status == LoanStatus.OpenForFunding
                : loan.BorrowerId == userId || contributions.Contains(loan.Id) || loan.Status == LoanStatus.OpenForFunding)
            .ToArray();
        var assessmentIds = results.Select(loan => loan.Id).ToArray();
        var assessments = await db.CreditAssessments.AsNoTracking().Where(item => assessmentIds.Contains(item.LoanId))
            .ToDictionaryAsync(item => item.LoanId, cancellationToken);
        return results.Select(loan => ToLoan(loan, assessments.GetValueOrDefault(loan.Id))).ToArray();
    }

    public async Task<LoanDto> GetLoanAsync(Guid userId, Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await FindLoanAsync(loanId, cancellationToken);
        var isParticipant = loan.BorrowerId == userId || await db.LoanContributions.AnyAsync(item => item.LoanId == loanId && item.LenderId == userId, cancellationToken);
        if (!isParticipant && loan.Status != LoanStatus.OpenForFunding) throw new KeyNotFoundException("Loan was not found.");
        var assessment = await db.CreditAssessments.AsNoTracking().SingleOrDefaultAsync(item => item.LoanId == loanId, cancellationToken);
        return ToLoan(loan, assessment);
    }

    public async Task<LoanDto> ContributeAsync(Guid userId, Guid loanId, decimal amount, bool acknowledgedTerms,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var existing = await db.LoanContributions.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.LoanId != loanId || existing.LenderId != userId || existing.Amount != amount)
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return await LoanResultAsync(await FindLoanAsync(loanId, cancellationToken), cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lender = await FindUserAsync(userId, cancellationToken);
        if (!lender.HasRole(UserRole.Lender)) throw new UnauthorizedAccessException("Lender role is required.");
        var loan = await FindLoanAsync(loanId, cancellationToken);
        if (loan.BorrowerId == userId) throw new InvalidOperationException("Borrowers cannot fund their own loan.");
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        var contribution = LoanContribution.Reserve(loanId, userId, amount, acknowledgedTerms, idempotencyKey, DateTimeOffset.UtcNow);
        loan.AddFunding(amount, DateTimeOffset.UtcNow);
        wallet.Debit(amount);
        var approval = new Approval(loanId, userId, ApprovalType.Contribution, amount, idempotencyKey, DateTimeOffset.UtcNow,
            $"Loan contribution for {loanId:N}");
        approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
        db.LoanContributions.Add(contribution);
        db.Approvals.Add(approval);
        db.LedgerEntries.Add(new LedgerEntry(userId, -amount, "LoanContributionReserved", contribution.Id,
            $"contribution-reserve:{contribution.Id:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoanResultAsync(loan, cancellationToken);
    }

    public async Task<LoanDto> WithdrawContributionAsync(Guid userId, Guid loanId, Guid contributionId,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var existing = await db.LedgerEntries.SingleOrDefaultAsync(entry => entry.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.ReferenceId != contributionId || existing.Type != "LoanContributionReleased")
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return await LoanResultAsync(await FindLoanAsync(loanId, cancellationToken), cancellationToken);
        }
        var existingApproval = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingApproval is not null) throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var loan = await FindLoanAsync(loanId, cancellationToken);
        var contribution = await db.LoanContributions.SingleOrDefaultAsync(item => item.Id == contributionId &&
            item.LoanId == loanId && item.LenderId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Contribution was not found.");
        loan.RemoveFunding(contribution.Amount);
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        wallet.Credit(contribution.Amount);
        contribution.Release();
        var approval = new Approval(loanId, userId, ApprovalType.ContributionWithdrawal, contribution.Amount,
            idempotencyKey, DateTimeOffset.UtcNow, $"Release contribution {contribution.Id:N}");
        approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
        db.Approvals.Add(approval);
        db.LedgerEntries.Add(new LedgerEntry(userId, contribution.Amount, "LoanContributionReleased", contribution.Id,
            idempotencyKey, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoanResultAsync(loan, cancellationToken);
    }

    public async Task<LoanDto> AcceptTermsAsync(Guid userId, Guid loanId, string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var existingApproval = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingApproval is not null)
        {
            if (existingApproval.UserId != userId || existingApproval.LoanId != loanId || existingApproval.Type != ApprovalType.BorrowerAcceptance)
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return await LoanResultAsync(await FindLoanAsync(loanId, cancellationToken), cancellationToken);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var loan = await FindLoanAsync(loanId, cancellationToken);
        if (loan.BorrowerId != userId) throw new UnauthorizedAccessException("Only the borrower can accept these terms.");
        loan.AcceptTerms(DateTimeOffset.UtcNow);
        db.LoanInstallments.AddRange(LoanInstallment.CreateSchedule(loan.Id, loan.TotalRepayable,
            loan.Installments, loan.AcceptedAt!.Value, loan.RepaymentDays));
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        wallet.Credit(loan.Principal);
        var contributions = await db.LoanContributions.Where(item => item.LoanId == loanId && item.Status == ContributionStatus.Reserved).ToListAsync(cancellationToken);
        foreach (var contribution in contributions) contribution.MarkDisbursed();
        var approval = new Approval(loanId, userId, ApprovalType.BorrowerAcceptance, loan.Principal, idempotencyKey, DateTimeOffset.UtcNow);
        approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
        db.Approvals.Add(approval);
        db.LedgerEntries.Add(new LedgerEntry(userId, loan.Principal, "LoanDisbursementSimulated", loanId,
            $"loan-disbursement:{loanId:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoanResultAsync(loan, cancellationToken);
    }

    public async Task<ApprovalDto> RequestRepaymentAsync(Guid userId, Guid loanId, decimal amount, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var existing = await db.Approvals.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.LoanId != loanId || existing.Amount != amount)
                throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            return ToApproval(existing);
        }
        var loan = await FindLoanAsync(loanId, cancellationToken);
        if (loan.BorrowerId != userId) throw new UnauthorizedAccessException("Only the borrower can request a repayment.");
        if (amount <= 0 || amount > loan.Outstanding || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment must be positive and cannot exceed the outstanding amount.");
        var approval = new Approval(loanId, userId, ApprovalType.Repayment, amount, idempotencyKey, DateTimeOffset.UtcNow);
        db.Approvals.Add(approval);
        await db.SaveChangesAsync(cancellationToken);
        return ToApproval(approval);
    }

    public async Task<IReadOnlyList<ApprovalDto>> GetPendingApprovalsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await db.Approvals.AsNoTracking().Where(item => item.UserId == userId && !item.Confirmed && !item.Rejected)
            .ToListAsync(cancellationToken)).OrderBy(item => item.CreatedAt).Select(ToApproval).ToArray();

    public async Task<ApprovalConfirmationDto> ConfirmApprovalAsync(Guid userId, Guid approvalId, string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateIdempotency(idempotencyKey);
        var approval = await db.Approvals.SingleOrDefaultAsync(item => item.Id == approvalId, cancellationToken)
            ?? throw new KeyNotFoundException("Approval was not found.");
        if (approval.UserId != userId) throw new UnauthorizedAccessException("This approval belongs to another user.");
        if (approval.Rejected) throw new InvalidOperationException("This approval was rejected and cannot be confirmed.");
        if (approval.Confirmed)
        {
            if (approval.Type != ApprovalType.BmoniTransfer && approval.ConfirmationIdempotencyKey != idempotencyKey)
                throw new InvalidOperationException("This approval was already confirmed with a different Idempotency-Key.");
            return await GetConfirmedApprovalResultAsync(approval, cancellationToken);
        }

        if (approval.Type == ApprovalType.BmoniTransfer)
        {
            var details = System.Text.Json.JsonSerializer.Deserialize<BmoniTransferDetails>(approval.Details ?? "{}")
                ?? throw new InvalidOperationException("BMONI transfer approval details are unavailable.");
            var result = await bmoni.TransferAsync(userId, details.ToBmoniUserId, approval.Amount, details.Description,
                $"approval:{approval.Id:N}", cancellationToken);
            if (result.Status == "COMPLETED")
            {
                approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }
            return new ApprovalConfirmationDto("BmoniTransfer", null, null, result, IsSimulated: false);
        }

        if (approval.Type == ApprovalType.WalletSpend)
        {
            await using var spendTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var replay = await db.LedgerEntries.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            var spendWallet = await GetWalletEntityAsync(userId, cancellationToken);
            if (replay is not null)
            {
                if (replay.UserId != userId || replay.ReferenceId != approval.Id || replay.Amount != -approval.Amount || replay.Type != "WalletSpendSimulated")
                    throw new InvalidOperationException("Idempotency-Key was already used for another operation.");
            }
            else
            {
                spendWallet.Debit(approval.Amount);
                db.LedgerEntries.Add(new LedgerEntry(userId, -approval.Amount, "WalletSpendSimulated", approval.Id,
                    idempotencyKey, DateTimeOffset.UtcNow));
            }
            approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            await spendTransaction.CommitAsync(cancellationToken);
            return new ApprovalConfirmationDto("WalletSpend", null, ToWallet(spendWallet), null, IsSimulated: true);
        }

        if (approval.Type != ApprovalType.Repayment || approval.LoanId is null)
            throw new InvalidOperationException("This approval type cannot be confirmed through this endpoint.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var loan = await FindLoanAsync(approval.LoanId.Value, cancellationToken);
        var wallet = await GetWalletEntityAsync(userId, cancellationToken);
        var contributions = (await db.LoanContributions.Where(item => item.LoanId == loan.Id && item.Status == ContributionStatus.Disbursed)
            .ToListAsync(cancellationToken)).OrderBy(item => item.CreatedAt).ToList();
        var groupedShares = contributions.GroupBy(item => item.LenderId)
            .Select(group => (LenderId: group.Key, Share: group.Sum(item => item.Amount), CreatedAt: group.Min(item => item.CreatedAt))).ToArray();
        var allocation = ProRataAllocator.Allocate(approval.Amount, groupedShares);
        var installments = await db.LoanInstallments.Where(item => item.LoanId == loan.Id)
            .OrderBy(item => item.Number).ToListAsync(cancellationToken);
        if (installments.Count != loan.Installments)
            throw new InvalidOperationException("Repayment schedule is unavailable for this loan.");
        var unapplied = approval.Amount;
        foreach (var installment in installments.Where(item => item.Outstanding > 0))
        {
            unapplied -= installment.ApplyPayment(unapplied);
            if (unapplied == 0) break;
        }
        if (unapplied != 0) throw new InvalidOperationException("Repayment exceeds the remaining installment schedule.");
        wallet.Debit(approval.Amount);
        loan.ApplyRepayment(approval.Amount);
        var repayment = new LoanRepayment(loan.Id, userId, approval.Amount, idempotencyKey, DateTimeOffset.UtcNow);
        db.LoanRepayments.Add(repayment);
        db.LedgerEntries.Add(new LedgerEntry(userId, -approval.Amount, "RepaymentSimulated", repayment.Id,
            $"{idempotencyKey}:borrower", DateTimeOffset.UtcNow));
        foreach (var share in allocation)
        {
            if (share.Amount <= 0) continue;
            var lenderWallet = await GetWalletEntityAsync(share.LenderId, cancellationToken);
            lenderWallet.Credit(share.Amount);
            db.RepaymentShares.Add(new RepaymentShare(repayment.Id, share.LenderId, share.Amount));
            db.LedgerEntries.Add(new LedgerEntry(share.LenderId, share.Amount, "RepaymentReceivedSimulated", repayment.Id,
                $"{idempotencyKey}:lender:{share.LenderId:N}", DateTimeOffset.UtcNow));
        }
        approval.Confirm(idempotencyKey, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ApprovalConfirmationDto("Repayment", await LoanResultAsync(loan, cancellationToken), ToWallet(wallet), null, true);
    }

    public async Task RejectApprovalAsync(Guid userId, Guid approvalId, CancellationToken cancellationToken)
    {
        var approval = await db.Approvals.SingleOrDefaultAsync(item => item.Id == approvalId, cancellationToken)
            ?? throw new KeyNotFoundException("Approval was not found.");
        if (approval.UserId != userId) throw new UnauthorizedAccessException("This approval belongs to another user.");
        if (approval.Type == ApprovalType.BmoniTransfer && await db.BmoniProposals.AnyAsync(item => item.IdempotencyKey == $"approval:{approval.Id:N}", cancellationToken))
            throw new InvalidOperationException("This BMONI transfer already has a remote proposal. Resolve its BMONI proposal status before rejecting the local approval.");
        approval.Reject();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var wallet = ToWallet(await GetWalletEntityAsync(userId, cancellationToken));
        var loans = (await db.Loans.AsNoTracking().Where(item => item.BorrowerId == userId ||
            db.LoanContributions.Any(contribution => contribution.LoanId == item.Id && contribution.LenderId == userId))
            .ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToList();
        var borrowed = loans.Where(item => item.BorrowerId == userId).ToArray();
        var lenderReturns = await GetLenderReturnsAsync(userId, cancellationToken);
        var recent = await GetTransactionsAsync(userId, DateTimeOffset.UtcNow.AddDays(-30), null, null, 1, 10, cancellationToken);
        var spendEntries = (await db.LedgerEntries.AsNoTracking().Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken)).Where(item => item.Type == "WalletSpendSimulated" && item.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-30)).ToList();
        var averageDailySpend = spendEntries.Sum(item => Math.Abs(item.Amount)) / 30m;
        var assessmentIds = loans.Select(loan => loan.Id).ToArray();
        var assessments = await db.CreditAssessments.AsNoTracking().Where(item => assessmentIds.Contains(item.LoanId))
            .ToDictionaryAsync(item => item.LoanId, cancellationToken);
        return new DashboardDto(wallet, loans.Count(item => item.Status == LoanStatus.Active),
            borrowed.Sum(item => item.Outstanding), lenderReturns.OutstandingExposure,
            loans.Select(loan => ToLoan(loan, assessments.GetValueOrDefault(loan.Id))).ToArray(),
            decimal.Round(averageDailySpend, 2), averageDailySpend > 0 ? decimal.Floor(wallet.Balance / averageDailySpend) : null,
            recent, true);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to,
        string? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page), "Page must be positive and pageSize must be 1-100.");
        if (from is not null && to is not null && from > to) throw new ArgumentException("The from date must be earlier than the to date.");
        var entries = await db.LedgerEntries.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        return entries.Where(item => (from is null || item.CreatedAt >= from) && (to is null || item.CreatedAt <= to) &&
                (string.IsNullOrWhiteSpace(type) || item.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new TransactionDto(item.Id, item.Amount, item.Type, item.ReferenceId, item.CreatedAt, item.IsSimulated)).ToArray();
    }

    public async Task<LenderReturnsDto> GetLenderReturnsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var principal = (await db.LoanContributions.AsNoTracking().Where(item => item.LenderId == userId && item.Status == ContributionStatus.Disbursed)
            .Select(item => item.Amount).ToListAsync(cancellationToken)).Sum();
        var returned = (await db.RepaymentShares.AsNoTracking().Where(item => item.LenderId == userId)
            .Select(item => item.Amount).ToListAsync(cancellationToken)).Sum();
        var principalReturned = Math.Min(principal, returned);
        return new LenderReturnsDto(principal, principalReturned, Math.Max(0m, returned - principal),
            Math.Max(0m, principal - principalReturned), true);
    }

    public async Task<CreditAssessmentDto> GetLoanAssessmentAsync(Guid userId, Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await FindLoanAsync(loanId, cancellationToken);
        if (loan.BorrowerId != userId) throw new UnauthorizedAccessException("Only the borrower can view this credit assessment.");
        var assessment = await db.CreditAssessments.AsNoTracking().SingleOrDefaultAsync(item => item.LoanId == loanId, cancellationToken)
            ?? throw new KeyNotFoundException("Credit assessment was not found.");
        return ToAssessment(assessment);
    }

    public async Task<ScoreDto> GetLatestScoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        var assessments = await db.CreditAssessments.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var latest = assessments.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        return latest is null
            ? new ScoreDto(50, "C", false, ["No completed loan assessment is available; the starter score is neutral and is not a guarantee."])
            : new ScoreDto(latest.FinalScore, Tier(latest.FinalScore), latest.AiAvailable, AssessmentReasons(latest));
    }

    private async Task<User> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
        ?? throw new KeyNotFoundException("User was not found.");

    private async Task<Loan> FindLoanAsync(Guid loanId, CancellationToken cancellationToken) =>
        await db.Loans.SingleOrDefaultAsync(loan => loan.Id == loanId, cancellationToken)
        ?? throw new KeyNotFoundException("Loan was not found.");

    private async Task<LoanDto> LoanResultAsync(Loan loan, CancellationToken cancellationToken)
    {
        var assessment = await db.CreditAssessments.AsNoTracking().SingleOrDefaultAsync(item => item.LoanId == loan.Id, cancellationToken);
        return ToLoan(loan, assessment);
    }

    private async Task<ApprovalConfirmationDto> GetConfirmedApprovalResultAsync(Approval approval, CancellationToken cancellationToken)
    {
        if (approval.Type == ApprovalType.BmoniTransfer)
        {
            var record = await db.BmoniProposals.AsNoTracking().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == $"approval:{approval.Id:N}", cancellationToken)
                ?? throw new InvalidOperationException("The confirmed BMONI proposal record is unavailable.");
            return new ApprovalConfirmationDto("BmoniTransfer", null, null,
                new BmoniTransferResult(record.BmoniProposalId ?? string.Empty, record.Status, IsSimulated: false), false);
        }
        if (approval.LoanId is { } loanId)
        {
            var loan = await FindLoanAsync(loanId, cancellationToken);
            return new ApprovalConfirmationDto(approval.Type.ToString(), await LoanResultAsync(loan, cancellationToken), null, null, true);
        }
        return new ApprovalConfirmationDto(approval.Type.ToString(), null,
            ToWallet(await GetWalletEntityAsync(approval.UserId, cancellationToken)), null, true);
    }

    private async Task<SimulatedWallet> GetWalletEntityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var wallet = await db.SimulatedWallets.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (wallet is not null) return wallet;
        if (!await db.Users.AnyAsync(item => item.Id == userId, cancellationToken)) throw new KeyNotFoundException("User was not found.");
        wallet = SimulatedWallet.Create(userId);
        db.SimulatedWallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static void ValidateIdempotency(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new ArgumentException("Idempotency-Key must be 1-200 characters.", nameof(key));
    }

    private static void ValidateCreateLoanRequest(CreateLoanRequest request)
    {
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount) throw new ArgumentOutOfRangeException(nameof(request.Amount));
        if (string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Length > 80) throw new ArgumentException("Purpose must be 1-80 characters.");
        if (string.IsNullOrWhiteSpace(request.Statement) || request.Statement.Length > 1000) throw new ArgumentException("Statement must be 1-1000 characters.");
        if (request.RepaymentDays is < 7 or > 120) throw new ArgumentOutOfRangeException(nameof(request.RepaymentDays));
        if (request.Installments is < 1 or > 4 || request.RepaymentDays < request.Installments) throw new ArgumentOutOfRangeException(nameof(request.Installments));
    }

    private static WalletDto ToWallet(SimulatedWallet wallet) => new(wallet.Balance, "NGN", true);
    private static ApprovalDto ToApproval(Approval approval) => new(approval.Id, approval.LoanId, approval.Amount,
        approval.Type.ToString(), approval.Confirmed, approval.Rejected, approval.Details, approval.CreatedAt);

    private sealed record BmoniTransferDetails(string ToBmoniUserId, string Description);

    private static LoanDto ToLoan(Loan loan, CreditAssessment? assessment = null)
    {
        var tier = Tier(loan.CreditScore);
        var schedule = new List<LoanScheduleDto>();
        var basePart = decimal.Floor(loan.TotalRepayable / loan.Installments * 100m) / 100m;
        var assigned = 0m;
        for (var index = 1; index <= loan.Installments; index++)
        {
            var installmentAmount = index == loan.Installments ? loan.TotalRepayable - assigned : basePart;
            assigned += installmentAmount;
            var dueDays = (int)Math.Ceiling((decimal)loan.RepaymentDays * index / loan.Installments);
            schedule.Add(new LoanScheduleDto(index, (loan.AcceptedAt ?? loan.CreatedAt).AddDays(dueDays), installmentAmount));
        }
        return new LoanDto(loan.Id, loan.Principal, loan.FundedAmount, loan.RemainingToFund,
            loan.Principal == 0 ? 0 : decimal.Round(loan.FundedAmount / loan.Principal * 100m, 2),
            loan.InterestRate, loan.TotalRepayable, loan.Outstanding, loan.CreditScore, tier, loan.Status.ToString(),
            loan.Purpose, loan.Statement, loan.Installments, loan.FundingDeadline, true, assessment?.AiAvailable ?? false,
            assessment is null ? ["Credit assessment details are unavailable."] : AssessmentReasons(assessment), schedule);
    }

    private static CreditAssessmentDto ToAssessment(CreditAssessment assessment) => new(assessment.LoanId,
        assessment.WalletSignalsScore, assessment.PurposeClarity, assessment.AmountRealism, assessment.RepaymentPlanFit,
        assessment.FinalScore, assessment.AiAvailable, AssessmentReasons(assessment));

    private static IReadOnlyList<string> AssessmentReasons(CreditAssessment assessment) =>
    [
        $"Wallet signals contributed {assessment.WalletSignalsScore}/100 based on deposit regularity, balance buffer, repayment history, and recent risk flags.",
        $"Purpose clarity assessment: {assessment.PurposeClarity}/100.",
        $"Requested amount realism assessment: {assessment.AmountRealism}/100.",
        $"Repayment plan fit assessment: {assessment.RepaymentPlanFit}/100.",
        assessment.AiAvailable ? "Claude contributed advisory sub-scores only; C# computed the final score and loan terms." : "Claude was unavailable; neutral 50/100 statement sub-scores were used.",
        "The final score weights deterministic wallet signals 80% and statement sub-scores 20%. A score is an assessment, not a guarantee."
    ];

    private static string Tier(int score) => score switch { >= 80 => "A", >= 65 => "B", >= 50 => "C", _ => "Declined" };
}
