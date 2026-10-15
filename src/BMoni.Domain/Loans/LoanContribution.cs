namespace BMoni.Domain.Loans;

public enum ContributionStatus { Reserved, Disbursed, Released }

public sealed class LoanContribution
{
    private LoanContribution() { }
    private LoanContribution(Guid id, Guid loanId, Guid lenderId, decimal amount, string idempotencyKey, DateTimeOffset createdAt)
    { Id = id; LoanId = loanId; LenderId = lenderId; Amount = amount; IdempotencyKey = idempotencyKey; CreatedAt = createdAt; }

    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid LenderId { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public ContributionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static LoanContribution Reserve(Guid loanId, Guid lenderId, decimal amount, bool acknowledgedTerms, string idempotencyKey, DateTimeOffset now)
    {
        if (!acknowledgedTerms) throw new InvalidOperationException("You must acknowledge the loan terms and default risk before contributing.");
        if (loanId == Guid.Empty || lenderId == Guid.Empty) throw new ArgumentException("Loan and lender are required.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        return new LoanContribution(Guid.NewGuid(), loanId, lenderId, amount, idempotencyKey, now);
    }

    public void MarkDisbursed()
    {
        if (Status != ContributionStatus.Reserved) throw new InvalidOperationException("Contribution is no longer reserved.");
        Status = ContributionStatus.Disbursed;
    }

    public void Release()
    {
        if (Status != ContributionStatus.Reserved) throw new InvalidOperationException("Contribution is no longer reserved.");
        Status = ContributionStatus.Released;
    }
}
