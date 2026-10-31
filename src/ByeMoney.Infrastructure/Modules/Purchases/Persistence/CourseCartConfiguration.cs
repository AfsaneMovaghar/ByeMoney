using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class CourseCartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("CourseCarts", table =>
        {
            table.HasCheckConstraint("CK_CourseCarts_Discounts",
                "\"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"ShippingAmountInNoor\" >= 0 AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"ShippingAmountInNoor\") = \"ShippingAmountInNoor\"");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.BuyerExternalUserId).HasMaxLength(100);
        b.Property(x => x.CouponCode).HasMaxLength(100);
        b.Property(x => x.LastSyncErrorCode).HasMaxLength(100);
        b.Property(x => x.RequestedItemSignatures).HasColumnType("text[]");
        b.Property(x => x.TopUpRequestIds).HasColumnType("uuid[]");
        b.Property(x => x.ShippingAmountInNoor).HasPrecision(18, 4).HasDefaultValue(0m);
        b.Property(x => x.ShippingRuleLabel).HasMaxLength(200);
        b.OwnsMany(x => x.Items, items =>
        {
            items.ToTable("CourseCartItems", table =>
            {
                table.HasCheckConstraint("CK_CourseCartItems_NonNegativeAmounts",
                    "\"PriceInNoor\" >= 0");
                table.HasCheckConstraint("CK_CourseCartItems_WholeNoorAmounts",
                    "trunc(\"PriceInNoor\") = \"PriceInNoor\"");
                table.HasCheckConstraint("CK_CourseCartItems_Quantity",
                    "\"Quantity\" >= 1");
                table.HasCheckConstraint("CK_CourseCartItems_Allocation",
                    "\"AllocatedAmountInNoor\" >= 0 AND trunc(\"AllocatedAmountInNoor\") = \"AllocatedAmountInNoor\" AND \"AllocatedAmountInNoor\" <= \"Quantity\" * \"PriceInNoor\"");
                table.HasCheckConstraint("CK_CourseCartItems_Shares",
                    "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND trunc(\"RegularAmountInNoor\") = \"RegularAmountInNoor\" AND trunc(\"GiftAmountInNoor\") = \"GiftAmountInNoor\" AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"AllocatedAmountInNoor\")");
            });
            items.WithOwner().HasForeignKey("CartId");
            items.HasKey("CartId", nameof(CartItem.ExternalCourseId), nameof(CartItem.ItemType));
            items.Property(x => x.ExternalCourseId).HasMaxLength(100);
            items.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ProductItemType.Course);
            items.Property(x => x.ParentExternalId).HasMaxLength(100);
            items.Property(x => x.Title).HasMaxLength(255).IsRequired();
            items.Property(x => x.PriceInNoor).HasPrecision(18, 4);
            items.Property(x => x.AllocatedAmountInNoor).HasPrecision(18, 4);
            items.Property(x => x.RegularAmountInNoor).HasPrecision(18, 4);
            items.Property(x => x.GiftAmountInNoor).HasPrecision(18, 4);
            items.Property(x => x.Quantity).HasDefaultValue(1).IsRequired();
        });
        b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Property(x => x.ItemDiscountInNoor).HasPrecision(18, 4);
        b.Property(x => x.CouponDiscountInNoor).HasPrecision(18, 4);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Ignore(x => x.Total);
        b.Ignore(x => x.OriginalTotal);
        b.HasIndex(x => new { x.Synchronized, x.UpdatedAtUtc });
        b.HasIndex(x => x.TopUpRequestId).IsUnique();
        b.HasIndex(x => x.BuyerId);
    }
}
