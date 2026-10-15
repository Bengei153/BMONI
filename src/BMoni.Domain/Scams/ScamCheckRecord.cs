namespace BMoni.Domain.Scams;

public sealed class ScamCheckRecord
{
    private ScamCheckRecord() { }

    public ScamCheckRecord(Guid userId, string riskLevel, string reasonTypes, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (riskLevel is not ("low" or "medium" or "high")) throw new ArgumentException("Risk level is invalid.", nameof(riskLevel));
        if (string.IsNullOrWhiteSpace(reasonTypes) || reasonTypes.Length > 500) throw new ArgumentException("Reason types are required.", nameof(reasonTypes));
        Id = Guid.NewGuid(); UserId = userId; RiskLevel = riskLevel; ReasonTypes = reasonTypes; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RiskLevel { get; private set; } = string.Empty;
    public string ReasonTypes { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
