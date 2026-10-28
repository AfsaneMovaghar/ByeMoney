using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Gifts;

public class GiftGrantBatchConfiguration : IEntityTypeConfiguration<GiftGrantBatch>
{
    public void Configure(EntityTypeBuilder<GiftGrantBatch> builder)
    {
        builder.ToTable("GiftGrantBatches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.RequestPayloadHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(b => b.Source)
            .IsRequired();

        builder.Property(b => b.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(b => b.AmountNoorPerUser)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(b => b.TotalAmountNoor)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(b => b.AdminUserId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, value => value.HasValue ? new UserId(value.Value) : null);

        builder.Property(b => b.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(b => b.IdempotencyKey)
            .IsUnique();

        builder.HasMany(b => b.Recipients)
            .WithOne()
            .HasForeignKey(r => r.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

