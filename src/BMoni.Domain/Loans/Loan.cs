namespace BMoni.Domain.Loans;

public enum LoanStatus { Declined, OpenForFunding, AwaitingBorrowerAcceptance, Active, Repaid, Expired, Cancelled, Defaulted }

public sealed class Loan
{
    private Loan() { }

    private Loan(Guid id, Guid borrowerId, decimal principal, string purpose, string statement, int repaymentDays,
        int installments, int creditScore, decimal interestRate, decimal totalRepayable, DateTimeOffset createdAt,
        DateTimeOffset fundingDeadline, LoanStatus status)
    {
        Id = id;
        BorrowerId = borrowerId;
        Principal = principal;
        Purpose = purpose;
        Statement = statement;
        RepaymentDays = repaymentDays;
        Installments = installments;
        CreditScore = creditScore;
        InterestRate = interestRate;
        TotalRepayable = totalRepayable;
        CreatedAt = createdAt;
        FundingDeadline = fundingDeadline;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid BorrowerId { get; private set; }
    public decimal Principal { get; private set; }
    public decimal FundedAmount { get; private set; }
    public decimal RepaidAmount { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string Statement { get; private set; } = string.Empty;
    public int RepaymentDays { get; private set; }
    public int Installments { get; private set; }
    public int CreditScore { get; private set; }
    public decimal InterestRate { get; private set; }
    public decimal TotalRepayable { get; private set; }
    public LoanStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset FundingDeadline { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public int Revision { get; private set; }
    public decimal RemainingToFund => Principal - FundedAmount;
    public decimal Outstanding => TotalRepayable - RepaidAmount;

    public static Loan Request(Guid borrowerId, decimal principal, string purpose, string statement, int repaymentDays,
        int installments, int creditScore, DateTimeOffset now)
    {
        if (borrowerId == Guid.Empty) throw new ArgumentException("Borrower is required.", nameof(borrowerId));
        if (principal <= 0 || decimal.Round(principal, 2) != principal) throw new ArgumentOutOfRangeException(nameof(principal));
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 80) throw new ArgumentException("Purpose must be 1-80 characters.", nameof(purpose));
        if (string.IsNullOrWhiteSpace(statement) || statement.Length > 1000) throw new ArgumentException("Statement must be 1-1000 characters.", nameof(statement));
        if (repaymentDays is < 7 or > 120) throw new ArgumentOutOfRangeException(nameof(repaymentDays));
        if (installments is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(installments));
        if (creditScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(creditScore));

        var (rate, limit) = creditScore switch
        {
            >= 80 => (0.03m, 20_000m),
            >= 65 => (0.05m, 10_000m),
            >= 50 => (0.08m, 5_000m),
            _ => (0m, 0m)
        };
        if (creditScore < 50) return new Loan(Guid.NewGuid(), borrowerId, principal, purpose.Trim(), statement.Trim(),
            repaymentDays, installments, creditScore, 0m, principal, now, now.AddDays(7), LoanStatus.Declined);
        if (principal > limit) throw new ArgumentOutOfRangeException(nameof(principal), $"Your score tier allows a maximum of ₦{limit:N0}.");
        var total = decimal.Round(principal * (1m + rate), 2, MidpointRounding.ToEven);
        return new Loan(Guid.NewGuid(), borrowerId, principal, purpose.Trim(), statement.Trim(), repaymentDays,
            installments, creditScore, rate, total, now, now.AddDays(7), LoanStatus.OpenForFunding);
    }

    public void AddFunding(decimal amount, DateTimeOffset now)
    {
        if (Status != LoanStatus.OpenForFunding) throw new InvalidOperationException("This loan is not open for funding.");
        if (now > FundingDeadline) throw new InvalidOperationException("Funding deadline has passed.");
        if (amount <= 0 || amount > RemainingToFund || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Contribution must be positive and cannot exceed the amount remaining.");
        FundedAmount += amount;
        if (FundedAmount == Principal) Status = LoanStatus.AwaitingBorrowerAcceptance;
        Revision++;
    }

    public void RemoveFunding(decimal amount)
    {
        if (Status != LoanStatus.OpenForFunding) throw new InvalidOperationException("Funding cannot be withdrawn after the loan is fully funded.");
        if (amount <= 0 || amount > FundedAmount) throw new ArgumentOutOfRangeException(nameof(amount));
        FundedAmount -= amount;
        Revision++;
    }

    public void AcceptTerms(DateTimeOffset now)
    {
        if (Status != LoanStatus.AwaitingBorrowerAcceptance) throw new InvalidOperationException("The loan is not ready for borrower acceptance.");
        if (now > FundingDeadline) throw new InvalidOperationException("The loan's funding deadline has passed.");
        AcceptedAt = now;
        Status = LoanStatus.Active;
        Revision++;
    }

    public void ApplyRepayment(decimal amount)
    {
        if (Status != LoanStatus.Active) throw new InvalidOperationException("Only an active loan can be repaid.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount || amount > Outstanding)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment must be positive, to 2 decimal places, and no greater than the outstanding amount.");
        RepaidAmount += amount;
        if (RepaidAmount == TotalRepayable) Status = LoanStatus.Repaid;
        Revision++;
    }

    public bool ExpireFunding(DateTimeOffset now)
    {
        if (Status != LoanStatus.OpenForFunding || now <= FundingDeadline) return false;
        Status = LoanStatus.Expired;
        Revision++;
        return true;
    }

    public void MarkDefaulted()
    {
        if (Status != LoanStatus.Active) return;
        Status = LoanStatus.Defaulted;
        Revision++;
    }
}
