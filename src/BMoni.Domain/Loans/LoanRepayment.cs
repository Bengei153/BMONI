namespace BMoni.Domain.Loans;

public sealed class LoanRepayment
{
    private LoanRepayment() { }
    public LoanRepayment(Guid loanId, Guid borrowerId, decimal amount, string idempotencyKey, DateTimeOffset createdAt)
    {
        if (loanId == Guid.Empty || borrowerId == Guid.Empty) throw new ArgumentException("Loan and borrower are required.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        Id = Guid.NewGuid(); LoanId = loanId; BorrowerId = borrowerId; Amount = amount; IdempotencyKey = idempotencyKey; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid BorrowerId { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsSimulated => true;
}
