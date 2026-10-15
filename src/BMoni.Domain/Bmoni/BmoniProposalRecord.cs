namespace BMoni.Domain.Bmoni;

public sealed class BmoniProposalRecord
{
    private BmoniProposalRecord() { }

    private BmoniProposalRecord(string idempotencyKey, string fromBmoniUserId, string ownerKeyId, string smartWalletId,
        string toBmoniUserId, decimal amount, string description, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IdempotencyKey = idempotencyKey;
        FromBmoniUserId = fromBmoniUserId;
        OwnerKeyId = ownerKeyId;
        SmartWalletId = smartWalletId;
        ToBmoniUserId = toBmoniUserId;
        Amount = amount;
        Description = description;
        Status = "CREATING";
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string FromBmoniUserId { get; private set; } = string.Empty;
    public string OwnerKeyId { get; private set; } = string.Empty;
    public string SmartWalletId { get; private set; } = string.Empty;
    public string ToBmoniUserId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? BmoniProposalId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static BmoniProposalRecord Begin(string idempotencyKey, string fromBmoniUserId, string ownerKeyId,
        string smartWalletId, string toBmoniUserId, decimal amount, string description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        return new BmoniProposalRecord(idempotencyKey, fromBmoniUserId, ownerKeyId, smartWalletId,
            toBmoniUserId, amount, description, now);
    }

    public void RecordProposal(string proposalId, string status, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(proposalId)) throw new ArgumentException("BMONI proposal id is required.", nameof(proposalId));
        BmoniProposalId = proposalId;
        Status = status;
        UpdatedAt = now;
    }

    public void RecordStatus(string status, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Proposal status is required.", nameof(status));
        Status = status;
        UpdatedAt = now;
    }

    public bool Matches(string fromBmoniUserId, string ownerKeyId, string smartWalletId, string toBmoniUserId,
        decimal amount, string description) => FromBmoniUserId == fromBmoniUserId && OwnerKeyId == ownerKeyId &&
        SmartWalletId == smartWalletId && ToBmoniUserId == toBmoniUserId && Amount == amount && Description == description;
}
