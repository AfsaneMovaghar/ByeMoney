using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class PhysicalProductPurchaseConfiguration : IEntityTypeConfiguration<PhysicalProductPurchase>
{
    public void Configure(EntityTypeBuilder<PhysicalProductPurchase> builder)
    {
        builder.ToTable("PhysicalProductPurchases", table =>
        {
            table.HasCheckConstraint("CK_PhysicalProductPurchases_NonNegativeAmounts",
                "\"UnitPriceInNoor\" >= 0 AND \"PaidAmountInNoor\" >= 0 AND trunc(\"UnitPriceInNoor\") = \"UnitPriceInNoor\" AND trunc(\"PaidAmountInNoor\") = \"PaidAmountInNoor\"");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_PaymentShares",
                "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"PaidAmountInNoor\")");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_Quantity", "\"Quantity\" >= 1");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_DeliveryStatus",
                "\"DeliveryStatus\" IN ('Preparing', 'Shipped', 'Delivered')");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_TrackingProvider",
                "\"TrackingProvider\" IS NULL OR \"TrackingProvider\" IN (" +
                string.Join(", ", PhysicalProductPurchase.AllowedTrackingProviders.Select(provider => $"'{provider}'")) + ")");
            // نشان «در انتظار پرداخت» فقط روی قلم آماده‌سازیِ دارای شناسهٔ مرسوله معنا دارد.
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CarrierPaymentPending",
                "\"CarrierPaymentPendingSinceUtc\" IS NULL OR (\"ProviderParcelId\" IS NOT NULL AND \"DeliveryStatus\" = 'Preparing')");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CarrierParcelQuantity",
                "\"CarrierParcelQuantity\" IS NULL OR \"CarrierParcelQuantity\" >= 1");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CancelledQuantity",
                "\"CancelledQuantity\" >= 0 AND \"CancelledQuantity\" <= \"Quantity\"");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_ParcelFacts",
                "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL AND \"ParcelBoxTypeId\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL AND \"ParcelBoxTypeId\" >= 1 AND \"ParcelBoxTypeId\" <= 12)");
            // سقف برگشت: جمع برگشت هر نوع نور از سهم پرداخت واقعی همان نوع بیشتر نمی‌شود؛ خرید بدون سهم برگشت ندارد.
            table.HasCheckConstraint("CK_PhysicalProductPurchases_RefundCap",
                "\"RefundedRegularInNoor\" >= 0 AND \"RefundedGiftInNoor\" >= 0 AND " +
                "\"RefundedRegularInNoor\" <= COALESCE(\"RegularAmountInNoor\", 0) AND " +
                "\"RefundedGiftInNoor\" <= COALESCE(\"GiftAmountInNoor\", 0)");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CarrierReturnResolution",
                "\"CarrierReturnResolution\" IS NULL OR \"CarrierReturnResolution\" IN ('DeliveryConfirmed', 'ClosedWithoutSettlement')");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CarrierAlertCode",
                "\"CarrierAlertCode\" IS NULL OR \"CarrierAlertCode\" IN (11304, 11305)");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_CarrierAlertResolution",
                "\"CarrierAlertResolution\" IS NULL OR (\"CarrierAlertCode\" IS NOT NULL AND \"CarrierAlertResolution\" IN ('DeliveryConfirmed', 'ClosedWithoutSettlement'))");
            table.HasCheckConstraint("CK_PhysicalProductPurchases_ScopeId", FinancialScope.Check());
            table.HasCheckConstraint("CK_PhysicalProductPurchases_SellerId", FinancialScope.Check("SellerId"));
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PhysicalProductPurchaseId(value))
            .IsRequired();

        builder.Property(x => x.BuyerId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(x => x.CartId).IsRequired();
        builder.Property(x => x.ScopeId).HasMaxLength(FinancialScope.MaxLength).IsRequired();
        builder.Property(x => x.SellerId).HasMaxLength(FinancialScope.MaxLength).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CartId);
        builder.HasIndex(x => x.BuyerId);

        builder.Property(x => x.ExternalProductId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductTitle).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.OwnsOne(x => x.Parcel, parcel =>
        {
            parcel.Property(x => x.WeightGrams).HasColumnName("ParcelWeightGrams");
            parcel.Property(x => x.LengthCm).HasColumnName("ParcelLengthCm");
            parcel.Property(x => x.WidthCm).HasColumnName("ParcelWidthCm");
            parcel.Property(x => x.HeightCm).HasColumnName("ParcelHeightCm");
            parcel.Property(x => x.DeclaredValueIrr).HasColumnName("ParcelDeclaredValueIrr");
            parcel.Property(x => x.IsFragile).HasColumnName("ParcelIsFragile");
            parcel.Property(x => x.BoxTypeId).HasColumnName("ParcelBoxTypeId");
        });
        builder.Property(x => x.UnitPriceInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.PaidAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.RegularAmountInNoor).HasPrecision(18, 4);
        builder.Property(x => x.GiftAmountInNoor).HasPrecision(18, 4);
        builder.Property(x => x.LedgerTransactionId);
        builder.Property(x => x.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PhysicalDeliveryStatus.Preparing)
            .IsRequired();
        builder.Property(x => x.ShippedAtUtc);
        builder.Property(x => x.DeliveredAtUtc);
        builder.Property(x => x.CarrierReturnedAtUtc);
        builder.Property(x => x.CarrierReturnResolution)
            .HasConversion<string>()
            .HasMaxLength(PhysicalProductPurchase.CarrierReturnResolutionMaxLength);
        builder.Property(x => x.CarrierReturnResolutionNote)
            .HasMaxLength(PhysicalProductPurchase.CarrierReturnResolutionNoteMaxLength);
        builder.Property(x => x.CarrierReturnResolvedAtUtc);
        builder.Property(x => x.CarrierReturnResolvedByUserId);
        builder.Property(x => x.CarrierAlertCode);
        builder.Property(x => x.CarrierAlertAtUtc);
        builder.Property(x => x.CarrierAlertResolution)
            .HasConversion<string>()
            .HasMaxLength(PhysicalProductPurchase.CarrierReturnResolutionMaxLength);
        builder.Property(x => x.CarrierAlertResolutionNote)
            .HasMaxLength(PhysicalProductPurchase.CarrierReturnResolutionNoteMaxLength);
        builder.Property(x => x.CarrierAlertResolvedAtUtc);
        builder.Property(x => x.CarrierAlertResolvedByUserId);
        builder.Property(x => x.TrackingUrl).HasMaxLength(PhysicalProductPurchase.TrackingUrlMaxLength);
        builder.Property(x => x.TrackingCode).HasMaxLength(PhysicalProductPurchase.TrackingCodeMaxLength);
        builder.Property(x => x.TrackingProvider).HasMaxLength(PhysicalProductPurchase.TrackingProviderMaxLength);
        builder.Property(x => x.ProviderParcelId).HasMaxLength(PhysicalProductPurchase.ProviderParcelIdMaxLength);
        builder.Property(x => x.CarrierParcelQuantity);
        builder.Property(x => x.CarrierPaymentPendingSinceUtc);
        builder.Property(x => x.ShipmentRequestStartedAtUtc);
        builder.HasIndex(x => new { x.DeliveryStatus, x.ShippedAtUtc });
        builder.HasIndex(x => x.TrackingCode);
        builder.Property(x => x.CancelledQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.RefundedRegularInNoor).HasPrecision(18, 4).HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.RefundedGiftInNoor).HasPrecision(18, 4).HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}

