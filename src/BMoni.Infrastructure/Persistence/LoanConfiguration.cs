using BMoni.Domain.Loans;
using BMoni.Domain.Wallets;
using BMoni.Domain.Scams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BMoni.Infrastructure.Persistence;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");
        builder.HasKey(loan => loan.Id);
        builder.Property(loan => loan.Principal).HasPrecision(18, 2);
        builder.Property(loan => loan.FundedAmount).HasPrecision(18, 2);
        builder.Property(loan => loan.RepaidAmount).HasPrecision(18, 2);
        builder.Property(loan => loan.InterestRate).HasPrecision(5, 4);
        builder.Property(loan => loan.TotalRepayable).HasPrecision(18, 2);
        builder.Property(loan => loan.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(loan => loan.Statement).HasMaxLength(1000).IsRequired();
        builder.Property(loan => loan.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(loan => loan.Revision).IsConcurrencyToken();
        builder.HasIndex(loan => new { loan.BorrowerId, loan.Status });
    }
}

internal sealed class LoanContributionConfiguration : IEntityTypeConfiguration<LoanContribution>
{
    public void Configure(EntityTypeBuilder<LoanContribution> builder)
    {
        builder.ToTable("loan_contributions");
        builder.HasKey(contribution => contribution.Id);
        builder.Property(contribution => contribution.Amount).HasPrecision(18, 2);
        builder.Property(contribution => contribution.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(contribution => contribution.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasOne<Loan>().WithMany().HasForeignKey(contribution => contribution.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(contribution => contribution.IdempotencyKey).IsUnique();
        builder.HasIndex(contribution => new { contribution.LoanId, contribution.LenderId, contribution.Status });
    }
}

internal sealed class LoanInstallmentConfiguration : IEntityTypeConfiguration<LoanInstallment>
{
    public void Configure(EntityTypeBuilder<LoanInstallment> builder)
    {
        builder.ToTable("loan_installments");
        builder.HasKey(installment => installment.Id);
        builder.Property(installment => installment.Amount).HasPrecision(18, 2);
        builder.Property(installment => installment.PaidAmount).HasPrecision(18, 2);
        builder.Property(installment => installment.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Loan>().WithMany().HasForeignKey(installment => installment.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(installment => new { installment.LoanId, installment.Number }).IsUnique();
    }
}

internal sealed class CreditAssessmentConfiguration : IEntityTypeConfiguration<CreditAssessment>
{
    public void Configure(EntityTypeBuilder<CreditAssessment> builder)
    {
        builder.ToTable("credit_assessments");
        builder.HasKey(assessment => assessment.Id);
        builder.HasOne<Loan>().WithMany().HasForeignKey(assessment => assessment.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BMoni.Domain.Users.User>().WithMany().HasForeignKey(assessment => assessment.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(assessment => assessment.LoanId).IsUnique();
        builder.HasIndex(assessment => new { assessment.UserId, assessment.CreatedAt });
    }
}

internal sealed class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> builder)
    {
        builder.ToTable("loan_repayments");
        builder.HasKey(repayment => repayment.Id);
        builder.Property(repayment => repayment.Amount).HasPrecision(18, 2);
        builder.Property(repayment => repayment.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(repayment => repayment.IdempotencyKey).IsUnique();
        builder.HasOne<Loan>().WithMany().HasForeignKey(repayment => repayment.LoanId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RepaymentShareConfiguration : IEntityTypeConfiguration<RepaymentShare>
{
    public void Configure(EntityTypeBuilder<RepaymentShare> builder)
    {
        builder.ToTable("repayment_shares");
        builder.HasKey(share => share.Id);
        builder.Property(share => share.Amount).HasPrecision(18, 2);
        builder.HasOne<LoanRepayment>().WithMany().HasForeignKey(share => share.RepaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("approvals");
        builder.HasKey(approval => approval.Id);
        builder.Property(approval => approval.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(approval => approval.Amount).HasPrecision(18, 2);
        builder.Property(approval => approval.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(approval => approval.Details).HasMaxLength(1000);
        builder.Property(approval => approval.ConfirmationIdempotencyKey).HasMaxLength(200);
        builder.HasIndex(approval => approval.IdempotencyKey).IsUnique();
        builder.HasIndex(approval => approval.ConfirmationIdempotencyKey).IsUnique();
        builder.HasOne<Loan>().WithMany().HasForeignKey(approval => approval.LoanId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SimulatedWalletConfiguration : IEntityTypeConfiguration<SimulatedWallet>
{
    public void Configure(EntityTypeBuilder<SimulatedWallet> builder)
    {
        builder.ToTable("simulated_wallets");
        builder.HasKey(wallet => wallet.UserId);
        builder.Property(wallet => wallet.Balance).HasPrecision(18, 2);
        builder.Property(wallet => wallet.Revision).IsConcurrencyToken();
        builder.HasOne<BMoni.Domain.Users.User>().WithOne().HasForeignKey<SimulatedWallet>(wallet => wallet.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<BMoni.Domain.Wallets.LedgerEntry>
{
    public void Configure(EntityTypeBuilder<BMoni.Domain.Wallets.LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Amount).HasPrecision(18, 2);
        builder.Property(entry => entry.Type).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(entry => entry.IdempotencyKey).IsUnique();
        builder.HasIndex(entry => new { entry.UserId, entry.CreatedAt });
        builder.HasOne<BMoni.Domain.Users.User>().WithMany().HasForeignKey(entry => entry.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScamCheckConfiguration : IEntityTypeConfiguration<ScamCheckRecord>
{
    public void Configure(EntityTypeBuilder<ScamCheckRecord> builder)
    {
        builder.ToTable("scam_checks");
        builder.HasKey(check => check.Id);
        builder.Property(check => check.RiskLevel).HasMaxLength(10).IsRequired();
        builder.Property(check => check.ReasonTypes).HasMaxLength(500).IsRequired();
        builder.HasOne<BMoni.Domain.Users.User>().WithMany().HasForeignKey(check => check.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(check => new { check.UserId, check.CreatedAt });
    }
}
