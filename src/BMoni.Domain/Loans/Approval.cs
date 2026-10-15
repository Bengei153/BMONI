namespace BMoni.Domain.Loans;

public enum ApprovalType { BorrowerAcceptance, Repayment, Contribution, ContributionWithdrawal, WalletDeposit, WalletSpend, BmoniTransfer }

public sealed class Approval
{
    private Approval() { }
    public Approval(Guid? loanId, Guid userId, ApprovalType type, decimal amount, string idempotencyKey,
        DateTimeOffset createdAt, string? details = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.");
        var loanBound = type is ApprovalType.BorrowerAcceptance or ApprovalType.Repayment or ApprovalType.Contribution or ApprovalType.ContributionWithdrawal;
        if ((loanBound && (!loanId.HasValue || loanId.Value == Guid.Empty)) || (!loanBound && loanId.HasValue))
            throw new ArgumentException("Approval loan reference does not match its operation type.", nameof(loanId));
        if (amount < 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        if (details?.Length > 1000) throw new ArgumentOutOfRangeException(nameof(details));
        Id = Guid.NewGuid(); LoanId = loanId; UserId = userId; Type = type; Amount = amount; IdempotencyKey = idempotencyKey; CreatedAt = createdAt; Details = details;
    }

    public Guid Id { get; private set; }
    public Guid? LoanId { get; private set; }
    public Guid UserId { get; private set; }
    public ApprovalType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public string? ConfirmationIdempotencyKey { get; private set; }
    public bool Confirmed { get; private set; }
    public bool Rejected { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public void Confirm(string confirmationIdempotencyKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(confirmationIdempotencyKey) || confirmationIdempotencyKey.Length > 200)
            throw new ArgumentException("A valid confirmation idempotency key is required.", nameof(confirmationIdempotencyKey));
        if (Rejected) throw new InvalidOperationException("A rejected approval cannot be confirmed.");
        if (Confirmed) throw new InvalidOperationException("Approval was already confirmed.");
        ConfirmationIdempotencyKey = confirmationIdempotencyKey;
        Confirmed = true;
        ConfirmedAt = now;
    }

    public void Reject()
    {
        if (Confirmed) throw new InvalidOperationException("A confirmed approval cannot be rejected.");
        if (Rejected) return;
        Rejected = true;
    }
}
