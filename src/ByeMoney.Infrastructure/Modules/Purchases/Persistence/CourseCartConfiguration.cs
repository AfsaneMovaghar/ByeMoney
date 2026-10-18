using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class CourseCartConfiguration : IEntityTypeConfiguration<CourseCart>
{
    public void Configure(EntityTypeBuilder<CourseCart> b)
    {
        b.ToTable("CourseCarts");
        b.HasKey(x => x.Id);
        b.Property(x => x.BuyerExternalUserId).HasMaxLength(100);
        b.Property(x => x.CouponCode).HasMaxLength(100);
        b.Property(x => x.LastSyncErrorCode).HasMaxLength(100);
        b.Property(x => x.ExternalCourseIds).HasColumnType("text[]");
        b.Property(x => x.TopUpRequestIds).HasColumnType("uuid[]");
        b.OwnsMany(x => x.Items, items =>
        {
            items.ToTable("CourseCartItems", table =>
            {
                table.HasCheckConstraint("CK_CourseCartItems_NonNegativeAmounts",
                    "\"BasePriceInNoor\" >= 0 AND \"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"PriceInNoor\" >= 0");
                table.HasCheckConstraint("CK_CourseCartItems_PriceBreakdown",
                    "\"BasePriceInNoor\" - \"ItemDiscountInNoor\" - \"CouponDiscountInNoor\" = \"PriceInNoor\"");
                table.HasCheckConstraint("CK_CourseCartItems_WholeNoorAmounts",
                    "trunc(\"BasePriceInNoor\") = \"BasePriceInNoor\" AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"PriceInNoor\") = \"PriceInNoor\"");
            });
            items.WithOwner().HasForeignKey("CartId");
            items.HasKey("CartId", nameof(CourseCartItem.ExternalCourseId));
            items.Property(x => x.ExternalCourseId).HasMaxLength(100);
            items.Property(x => x.Title).HasMaxLength(255).IsRequired();
            items.Property(x => x.BasePriceInNoor).HasPrecision(18, 4);
            items.Property(x => x.ItemDiscountInNoor).HasPrecision(18, 4);
            items.Property(x => x.CouponDiscountInNoor).HasPrecision(18, 4);
            items.Property(x => x.PriceInNoor).HasPrecision(18, 4);
        });
        b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Ignore(x => x.Total);
        b.HasIndex(x => new { x.Synchronized, x.UpdatedAtUtc });
        b.HasIndex(x => x.TopUpRequestId).IsUnique();
        b.HasIndex(x => x.BuyerId);
    }
}
