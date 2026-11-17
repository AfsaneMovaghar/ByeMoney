using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("Carts", table =>
        {
            table.HasCheckConstraint("CK_Carts_Discounts",
                "\"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"ShippingAmountInNoor\" >= 0 AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"ShippingAmountInNoor\") = \"ShippingAmountInNoor\"");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.BuyerExternalUserId).HasMaxLength(100);
        b.Property(x => x.CouponCode).HasMaxLength(100);
        b.Property(x => x.LastSyncErrorCode).HasMaxLength(100);
        b.Property(x => x.RequestedItemSignatures).HasColumnType("text[]");
        b.Property(x => x.ShippingAmountInNoor).HasPrecision(18, 4).HasDefaultValue(0m);
        b.Property(x => x.ShippingRuleLabel).HasMaxLength(200);
        b.Property(x => x.ShippingOriginId).HasMaxLength(Cart.ShippingOriginIdMaxLength);
        b.Property(x => x.ShippingOriginLabel).HasMaxLength(Cart.ShippingOriginLabelMaxLength);
        b.Property(x => x.AdminFreeReason).HasMaxLength(Cart.AdminFreeReasonMaxLength);
        b.Ignore(x => x.IsAdminFree);
        b.OwnsOne(x => x.ShippingAddress, address =>
        {
            address.Property(x => x.Version).HasMaxLength(ShippingAddress.VersionMaxLength);
            address.Property(x => x.Province).HasMaxLength(ShippingAddress.ProvinceMaxLength);
            address.Property(x => x.City).HasMaxLength(ShippingAddress.CityMaxLength);
            address.Property(x => x.PostalCode).HasMaxLength(ShippingAddress.PostalCodeMaxLength);
            address.Property(x => x.FullAddress).HasMaxLength(ShippingAddress.FullAddressMaxLength);
            address.Property(x => x.RecipientName).HasMaxLength(ShippingAddress.RecipientNameMaxLength);
            address.Property(x => x.RecipientPhone).HasMaxLength(ShippingAddress.RecipientPhoneMaxLength);
            address.Property(x => x.CityDocumentId).HasMaxLength(ShippingAddress.CityDocumentIdMaxLength);
        });
        b.OwnsMany(x => x.Items, items =>
        {
            items.ToTable("CartItems", table =>
            {
                table.HasCheckConstraint("CK_CartItems_NonNegativeAmounts",
                    "\"PriceInNoor\" >= 0");
                table.HasCheckConstraint("CK_CartItems_WholeNoorAmounts",
                    "trunc(\"PriceInNoor\") = \"PriceInNoor\"");
                table.HasCheckConstraint("CK_CartItems_Quantity",
                    "\"Quantity\" >= 1");
                table.HasCheckConstraint("CK_CartItems_Allocation",
                    "\"AllocatedAmountInNoor\" >= 0 AND trunc(\"AllocatedAmountInNoor\") = \"AllocatedAmountInNoor\" AND \"AllocatedAmountInNoor\" <= \"Quantity\" * \"PriceInNoor\"");
            });
            items.WithOwner().HasForeignKey("CartId");
            items.HasKey("CartId", nameof(CartItem.ExternalItemId), nameof(CartItem.ItemType));
            items.Property(x => x.ExternalItemId).HasMaxLength(100);
            items.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ProductItemType.Course);
            items.Property(x => x.ParentExternalId).HasMaxLength(100);
            items.Property(x => x.Title).HasMaxLength(255).IsRequired();
            items.Property(x => x.PriceInNoor).HasPrecision(18, 4);
            items.Property(x => x.AllocatedAmountInNoor).HasPrecision(18, 4);
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
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
