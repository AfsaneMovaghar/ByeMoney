using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class CoursePurchaseConfiguration : IEntityTypeConfiguration<CoursePurchase>
{
    public void Configure(EntityTypeBuilder<CoursePurchase> builder)
    {
        builder.ToTable("CoursePurchases");

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Id)
            .HasConversion(
                id => id.Value,
                value => new CoursePurchaseId(value))
            .IsRequired();

        builder.Property(cp => cp.BuyerId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(cp => cp.Status)
            .IsRequired();

        builder.Property(cp => cp.LedgerTransactionId);

        builder.Property(cp => cp.NotificationAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(cp => cp.LastNotificationAttemptAtUtc);

        builder.Property(cp => cp.NotificationFailureReason)
            .HasMaxLength(1000);

        builder.Property(cp => cp.CompletedAtUtc);

        builder.Property(cp => cp.CreatedAtUtc)
            .IsRequired();

        builder.Property(cp => cp.UpdatedAt);

        builder.OwnsOne(cp => cp.Snapshot, snapshotBuilder =>
        {
            snapshotBuilder.Property(s => s.ProductSource)
                .HasColumnName("ProductSource")
                .HasMaxLength(50)
                .IsRequired();

            snapshotBuilder.Property(s => s.ExternalProductId)
                .HasColumnName("ExternalProductId")
                .HasMaxLength(100)
                .IsRequired();

            snapshotBuilder.Property(s => s.ProductTitle)
                .HasColumnName("ProductTitle")
                .HasMaxLength(255)
                .IsRequired();

            snapshotBuilder.Property(s => s.PriceInRialAtPurchaseTime)
                .HasColumnName("PriceInRialAtPurchaseTime")
                .HasPrecision(18, 2)
                .IsRequired();

            snapshotBuilder.Property(s => s.ConversionRateAtPurchaseTime)
                .HasColumnName("ConversionRateAtPurchaseTime")
                .HasPrecision(18, 4)
                .IsRequired();

            snapshotBuilder.Property(s => s.PriceInNoorAtPurchaseTime)
                .HasColumnName("PriceInNoorAtPurchaseTime")
                .HasPrecision(18, 4)
                .IsRequired();

            snapshotBuilder.Property(s => s.PurchasedAt)
                .HasColumnName("PurchasedAt")
                .IsRequired();
        });

        builder.HasIndex(cp => cp.BuyerId);
        builder.HasIndex(cp => cp.Status);
    }
}