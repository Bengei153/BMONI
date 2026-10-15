using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BMoni.Application.Bmoni;
using BMoni.Domain.Bmoni;
using BMoni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BMoni.Infrastructure.Bmoni;

internal sealed class BmoniWebhookService(AppDbContext db, IConfiguration configuration) : IBmoniWebhookService
{
    public async Task<BmoniWebhookReceiveResult> ReceiveAsync(byte[] rawBody, string? signature, string? webhookId,
        CancellationToken cancellationToken)
    {
        var secret = configuration["BMONI_WEBHOOK_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            return new BmoniWebhookReceiveResult(false, false, "Webhook verification is not configured.");
        if (rawBody.Length is 0 or > 1_048_576)
            return new BmoniWebhookReceiveResult(false, false, "Webhook body size is invalid.");

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody);
        if (string.IsNullOrWhiteSpace(signature) || !TryDecodeHex(signature, out var supplied) ||
            supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
            return new BmoniWebhookReceiveResult(false, false, "Webhook signature is invalid.");

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var eventId = ReadString(root, "id");
            var eventType = ReadString(root, "eventType");
            var timestamp = ReadString(root, "timestamp");
            var payload = root.TryGetProperty("payload", out var payloadValue) ? payloadValue : default;
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType) || timestamp is null || payload.ValueKind != JsonValueKind.Object)
                return new BmoniWebhookReceiveResult(false, false, "Webhook envelope is incomplete.");
            if (eventId.Length > 200 || eventType.Length > 160 ||
                !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var occurredAt))
                return new BmoniWebhookReceiveResult(false, false, "Webhook envelope fields are invalid.");
            if (string.IsNullOrWhiteSpace(webhookId) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(webhookId), Encoding.UTF8.GetBytes(eventId)))
                return new BmoniWebhookReceiveResult(false, false, "Webhook id header does not match the event id.");

            var userId = payload.TryGetProperty("userId", out var user) && user.ValueKind == JsonValueKind.String ? user.GetString() : null;
            var status = payload.TryGetProperty("status", out var statusValue) && statusValue.ValueKind == JsonValueKind.String ? statusValue.GetString() : null;
            if (await db.BmoniWebhookEvents.AnyAsync(item => item.EventId == eventId, cancellationToken))
                return new BmoniWebhookReceiveResult(true, true);

            db.BmoniWebhookEvents.Add(new BmoniWebhookEvent(eventId, eventType, userId, status, occurredAt, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            return new BmoniWebhookReceiveResult(true, false);
        }
        catch (JsonException)
        {
            return new BmoniWebhookReceiveResult(false, false, "Webhook body is not valid JSON.");
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromHexString(value); return true; }
        catch (FormatException) { bytes = []; return false; }
    }
}
