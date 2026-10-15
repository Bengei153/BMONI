using BMoni.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BMoni.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.Property(user => user.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(100).IsRequired();
        builder.Property(user => user.Roles).HasConversion<int>().IsRequired();
        builder.Property(user => user.BmoniUserId).HasMaxLength(128);
        builder.Property(user => user.BmoniOwnerAddress).HasMaxLength(128);
        builder.Property(user => user.BmoniSmartWalletId).HasMaxLength(128);
        builder.Property(user => user.BmoniWalletAddress).HasMaxLength(128);
        builder.Property(user => user.BmoniOnboardingStatus).HasMaxLength(40);
        builder.Property(user => user.CreatedAt).IsRequired();
    }
}
