using BMoni.Domain.Loans;
using BMoni.Domain.Users;
using BMoni.Application.Bmoni;

namespace BMoni.Application.Loans;

public sealed record DemoAccountDto(Guid Id, string Email, string Password, string FirstName, UserRole Roles);
public sealed record BootstrapResponse(bool Ready, bool IsSandbox, bool UsesSimulatedFunds, bool BmoniConfigured,
    bool ClaudeAvailable, IReadOnlyList<DemoAccountDto> DemoAccounts, object LoanRules, string Disclaimer);
public sealed record WalletDto(decimal Balance, string Currency, bool IsSimulated);
public sealed record DepositRequest(decimal Amount);
public sealed record WalletSpendRequest(decimal Amount, string Description);
public sealed record CreateLoanRequest(decimal Amount, string Purpose, string Statement, int RepaymentDays, int Installments);
public sealed record ContributeRequest(decimal Amount, bool AcknowledgedTerms);
public sealed record RepaymentRequest(decimal Amount);
public sealed record LoanScheduleDto(int Installment, DateTimeOffset DueAt, decimal Amount);
public sealed record LoanDto(Guid Id, decimal Amount, decimal Funded, decimal Remaining, decimal ProgressPercent,
    decimal InterestRate, decimal TotalRepayable, decimal Outstanding, int Score, string Tier, string Status,
    string Purpose, string Statement, int Installments, DateTimeOffset FundingDeadline, bool IsSimulated,
    bool AiAvailable, IReadOnlyList<string> ScoreReasons, IReadOnlyList<LoanScheduleDto> Schedule);
public sealed record CreditAssessmentDto(Guid LoanId, int WalletSignalsScore, int PurposeClarity, int AmountRealism,
    int RepaymentPlanFit, int FinalScore, bool AiAvailable, IReadOnlyList<string> Reasons);
public sealed record ScoreDto(int Score, string Tier, bool AiAvailable, IReadOnlyList<string> Reasons);
public sealed record ApprovalDto(Guid Id, Guid? LoanId, decimal Amount, string Type, bool Confirmed, bool Rejected,
    string? Details, DateTimeOffset CreatedAt);
public sealed record ApprovalConfirmationDto(string Operation, LoanDto? Loan, WalletDto? Wallet,
    BmoniTransferResult? BmoniTransfer, bool IsSimulated);
public sealed record TransactionDto(Guid Id, decimal Amount, string Type, Guid ReferenceId, DateTimeOffset CreatedAt, bool IsSimulated);
public sealed record LenderReturnsDto(decimal PrincipalCommitted, decimal PrincipalReturned, decimal InterestEarned,
    decimal OutstandingExposure, bool IsSimulated);
public sealed record DashboardDto(WalletDto Wallet, int ActiveLoans, decimal OutstandingBorrowed, decimal OutstandingLent,
    IReadOnlyList<LoanDto> Loans, decimal AverageDailySpend, decimal? RunwayDays,
    IReadOnlyList<TransactionDto> RecentTransactions, bool IsSimulated);

public interface IPlatformService
{
    Task<BootstrapResponse> BootstrapAsync(CancellationToken cancellationToken);
    Task<WalletDto> GetWalletAsync(Guid userId, CancellationToken cancellationToken);
    Task<WalletDto> DepositAsync(Guid userId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);
    Task<ApprovalDto> RequestWalletSpendAsync(Guid userId, decimal amount, string description, string idempotencyKey, CancellationToken cancellationToken);
    Task<ApprovalDto> RequestBmoniTransferAsync(Guid userId, string toBmoniUserId, decimal amount, string description, string idempotencyKey, CancellationToken cancellationToken);
    Task<LoanDto> CreateLoanAsync(Guid userId, CreateLoanRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanDto>> GetLoansAsync(Guid userId, string? role, CancellationToken cancellationToken);
    Task<LoanDto> GetLoanAsync(Guid userId, Guid loanId, CancellationToken cancellationToken);
    Task<LoanDto> ContributeAsync(Guid userId, Guid loanId, decimal amount, bool acknowledgedTerms, string idempotencyKey, CancellationToken cancellationToken);
    Task<LoanDto> WithdrawContributionAsync(Guid userId, Guid loanId, Guid contributionId, string idempotencyKey, CancellationToken cancellationToken);
    Task<LoanDto> AcceptTermsAsync(Guid userId, Guid loanId, string idempotencyKey, CancellationToken cancellationToken);
    Task<ApprovalDto> RequestRepaymentAsync(Guid userId, Guid loanId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalDto>> GetPendingApprovalsAsync(Guid userId, CancellationToken cancellationToken);
    Task<ApprovalConfirmationDto> ConfirmApprovalAsync(Guid userId, Guid approvalId, string idempotencyKey, CancellationToken cancellationToken);
    Task RejectApprovalAsync(Guid userId, Guid approvalId, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to,
        string? type, int page, int pageSize, CancellationToken cancellationToken);
    Task<LenderReturnsDto> GetLenderReturnsAsync(Guid userId, CancellationToken cancellationToken);
    Task<CreditAssessmentDto> GetLoanAssessmentAsync(Guid userId, Guid loanId, CancellationToken cancellationToken);
    Task<ScoreDto> GetLatestScoreAsync(Guid userId, CancellationToken cancellationToken);
}
