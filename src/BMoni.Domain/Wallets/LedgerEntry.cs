namespace BMoni.Domain.Wallets;

public sealed class LedgerEntry
{
    private LedgerEntry() { }
    public LedgerEntry(Guid userId, decimal amount, string type, Guid referenceId, string idempotencyKey, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty || referenceId == Guid.Empty) throw new ArgumentException("User and reference are required.");
        if (amount == 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(type) || type.Length > 40) throw new ArgumentException("Entry type must be 1-40 characters.", nameof(type));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        Id = Guid.NewGuid(); UserId = userId; Amount = amount; Type = type; ReferenceId = referenceId; IdempotencyKey = idempotencyKey; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsSimulated => true;
}
