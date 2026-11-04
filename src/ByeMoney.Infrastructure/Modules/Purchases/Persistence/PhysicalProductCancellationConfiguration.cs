using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class PhysicalProductCancellationConfiguration : IEntityTypeConfiguration<PhysicalProductCancellation>
{
    public void Configure(EntityTypeBuilder<PhysicalProductCancellation> builder)
    {
        builder.ToTable("PhysicalProductCancellations", table =>
        {
            table.HasCheckConstraint("CK_PhysicalProductCancellations_Quantity", "\"Quantity\" >= 1");
            table.HasCheckConstraint("CK_PhysicalProductCancellations_NonNegativeRefunds",
                "\"RegularRefundInNoor\" >= 0 AND \"GiftRefundInNoor\" >= 0 AND " +
                "\"ShippingRegularRefundInNoor\" >= 0 AND \"ShippingGiftRefundInNoor\" >= 0");
            table.AddEnumCheckConstraint<PhysicalProductCancellation, PhysicalCancellationActor>(
                "CK_PhysicalProductCancellations_Actor",
                nameof(PhysicalProductCancellation.Actor));
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PhysicalProductCancellationId(value))
            .IsRequired();

        builder.Property(x => x.PurchaseId)
            .HasConversion(id => id.Value, value => new PhysicalProductPurchaseId(value))
            .IsRequired();

        builder.Property(x => x.PerformedByUserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.HasOne<PhysicalProductPurchase>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // تکرار یا هم‌زمانی همان عملیات روی همان قلم دومین ردیف و برگشت مضاعف نمی‌سازد.
        builder.HasIndex(x => new { x.OperationId, x.PurchaseId }).IsUnique();

        // برگشت ارسال هر سبد فقط یک بار ثبت می‌شود.
        builder.HasIndex(x => x.CartId)
            .IsUnique()
            .HasFilter("\"ShippingRegularRefundInNoor\" + \"ShippingGiftRefundInNoor\" > 0")
            .HasDatabaseName("IX_PhysicalProductCancellations_CartId_ShippingRefund");

        builder.HasIndex(x => x.PurchaseId);

        // صف برگشت موجودی استرپی: فقط عملیات‌هایی که هنوز تأیید برگشت موجودی ندارند.
        builder.HasIndex(x => x.OperationId)
            .HasFilter("\"StockReturnedAtUtc\" IS NULL")
            .HasDatabaseName("IX_PhysicalProductCancellations_PendingStockReturn");

        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.RegularRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.GiftRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.ShippingRegularRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.ShippingGiftRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Actor).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.StockReturnedAtUtc);
        builder.Property(x => x.StockReturnErrorCode).HasMaxLength(50);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}
