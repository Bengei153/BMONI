using BMoni.Domain.Users;
using BMoni.Domain.Bmoni;
using BMoni.Domain.Loans;
using BMoni.Domain.Wallets;
using BMoni.Domain.Scams;
using Microsoft.EntityFrameworkCore;

namespace BMoni.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BmoniProposalRecord> BmoniProposals => Set<BmoniProposalRecord>();
    public DbSet<BmoniWebhookEvent> BmoniWebhookEvents => Set<BmoniWebhookEvent>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<CreditAssessment> CreditAssessments => Set<CreditAssessment>();
    public DbSet<LoanContribution> LoanContributions => Set<LoanContribution>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<RepaymentShare> RepaymentShares => Set<RepaymentShare>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<SimulatedWallet> SimulatedWallets => Set<SimulatedWallet>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<ScamCheckRecord> ScamChecks => Set<ScamCheckRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
