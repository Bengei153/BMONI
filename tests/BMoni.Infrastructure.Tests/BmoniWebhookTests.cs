using System.Security.Cryptography;
using System.Text;
using BMoni.Domain.Bmoni;
using BMoni.Infrastructure.Bmoni;
using BMoni.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BMoni.Infrastructure.Tests;

public sealed class BmoniWebhookTests
{
    [Fact]
    public async Task Receiver_verifies_raw_body_and_deduplicates_event_id_without_storing_raw_payload()
    {
        const string secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var rawBody = Encoding.UTF8.GetBytes("{\"id\":\"event-123\", \"eventType\":\"onboarding.completed\", \"payload\":{\"userId\":\"bmoni-user-1\",\"sensitiveName\":\"must-not-be-stored\"}, \"timestamp\":\"2026-10-15T12:00:00Z\"}");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody)).ToLowerInvariant();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["BMONI_WEBHOOK_SECRET"] = secret }).Build();
        var receiver = new BmoniWebhookService(db, configuration);

        var first = await receiver.ReceiveAsync(rawBody, signature, "event-123", CancellationToken.None);
        var replay = await receiver.ReceiveAsync(rawBody, signature, "event-123", CancellationToken.None);
        var invalid = await receiver.ReceiveAsync(rawBody, "00", "event-123", CancellationToken.None);
        var stored = await db.BmoniWebhookEvents.SingleAsync();

        Assert.True(first.Accepted);
        Assert.False(first.Duplicate);
        Assert.True(replay.Accepted);
        Assert.True(replay.Duplicate);
        Assert.False(invalid.Accepted);
        Assert.Equal("bmoni-user-1", stored.BmoniUserId);
        Assert.Equal("onboarding.completed", stored.EventType);
        Assert.Null(typeof(BmoniWebhookEvent).GetProperty("RawPayload"));
    }
}
