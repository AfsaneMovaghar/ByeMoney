using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Gifts;

public class GiftGrantRecipientConfiguration : IEntityTypeConfiguration<GiftGrantRecipient>
{
    public void Configure(EntityTypeBuilder<GiftGrantRecipient> builder)
    {
        builder.ToTable("GiftGrantRecipients");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(r => r.ExternalUserId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.AmountNoor)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(r => r.NewGiftBalance)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(r => r.TransactionId)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.GrantedAtUtc)
            .IsRequired();

        builder.HasIndex(r => r.BatchId);

        builder.HasIndex(r => r.UserId);

        builder.HasIndex(r => r.ExternalUserId);
    }
}

