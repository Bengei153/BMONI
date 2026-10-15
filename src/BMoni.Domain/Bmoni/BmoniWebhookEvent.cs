namespace BMoni.Domain.Bmoni;

public sealed class BmoniWebhookEvent
{
    private BmoniWebhookEvent() { }

    public BmoniWebhookEvent(string eventId, string eventType, string? bmoniUserId, string? status,
        DateTimeOffset occurredAt, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 200) throw new ArgumentException("A valid webhook event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 160) throw new ArgumentException("A valid webhook event type is required.", nameof(eventType));
        if (bmoniUserId?.Length > 128) throw new ArgumentOutOfRangeException(nameof(bmoniUserId));
        if (status?.Length > 80) throw new ArgumentOutOfRangeException(nameof(status));
        Id = Guid.NewGuid();
        EventId = eventId;
        EventType = eventType;
        BmoniUserId = bmoniUserId;
        Status = status;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; private set; }
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string? BmoniUserId { get; private set; }
    public string? Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkProcessed(DateTimeOffset now) => ProcessedAt ??= now;
}
