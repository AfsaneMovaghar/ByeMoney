using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class OrderSettlementItemConfiguration : IEntityTypeConfiguration<OrderSettlementItem>
{
    /// <summary>
    /// کلید اصلی شناسهٔ سفارش است: هر سفارش حداکثر در یک دوره ثبت می‌شود و
    /// بستن هم‌زمان دو ادمین با نقض همین قید شکست می‌خورد.
    /// </summary>
    public const string PrimaryKeyName = "PK_OrderSettlementItems";

    public void Configure(EntityTypeBuilder<OrderSettlementItem> builder)
    {
        builder.ToTable("OrderSettlementItems", table =>
        {
            table.HasCheckConstraint("CK_OrderSettlementItems_NetShares",
                "\"RegularNetAmountInNoor\" >= 0 AND \"GiftNetAmountInNoor\" >= 0 AND " +
                "\"NetAmountInNoor\" = \"RegularNetAmountInNoor\" + \"GiftNetAmountInNoor\"");
        });

        builder.HasKey(x => x.OrderId).HasName(PrimaryKeyName);
        builder.Property(x => x.OrderId)
            .HasMaxLength(OrderSettlementItem.OrderIdMaxLength)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.PeriodId)
            .HasConversion(id => id.Value, value => new OrderSettlementPeriodId(value))
            .IsRequired();

        builder.Property(x => x.SourceType).HasMaxLength(OrderSettlementItem.SourceTypeMaxLength).IsRequired();
        builder.Property(x => x.BuyerExternalUserId)
            .HasMaxLength(OrderSettlementItem.BuyerExternalUserIdMaxLength)
            .IsRequired();
        builder.Property(x => x.OrderCreatedAtUtc).IsRequired();
        builder.Property(x => x.RegularNetAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.GiftNetAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.NetAmountInNoor).HasPrecision(18, 4).IsRequired();

        builder.HasIndex(x => new { x.PeriodId, x.OrderCreatedAtUtc });
    }
}
