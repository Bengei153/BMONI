using BMoni.Domain.Bmoni;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BMoni.Infrastructure.Persistence;

internal sealed class BmoniProposalConfiguration : IEntityTypeConfiguration<BmoniProposalRecord>
{
    public void Configure(EntityTypeBuilder<BmoniProposalRecord> builder)
    {
        builder.ToTable("bmoni_proposals");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(record => record.IdempotencyKey).IsUnique();
        builder.Property(record => record.FromBmoniUserId).HasMaxLength(128).IsRequired();
        builder.Property(record => record.OwnerKeyId).HasMaxLength(255).IsRequired();
        builder.Property(record => record.SmartWalletId).HasMaxLength(128).IsRequired();
        builder.Property(record => record.ToBmoniUserId).HasMaxLength(128).IsRequired();
        builder.Property(record => record.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(record => record.Description).HasMaxLength(500).IsRequired();
        builder.Property(record => record.BmoniProposalId).HasMaxLength(128);
        builder.Property(record => record.Status).HasMaxLength(40).IsRequired();
        builder.Property(record => record.CreatedAt).IsRequired();
        builder.Property(record => record.UpdatedAt).IsRequired();
    }
}

internal sealed class BmoniWebhookEventConfiguration : IEntityTypeConfiguration<BmoniWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BmoniWebhookEvent> builder)
    {
        builder.ToTable("bmoni_webhook_events");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EventId).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => item.EventId).IsUnique();
        builder.Property(item => item.EventType).HasMaxLength(160).IsRequired();
        builder.Property(item => item.BmoniUserId).HasMaxLength(128);
        builder.Property(item => item.Status).HasMaxLength(80);
        builder.Property(item => item.OccurredAt).IsRequired();
        builder.Property(item => item.ReceivedAt).IsRequired();
    }
}
