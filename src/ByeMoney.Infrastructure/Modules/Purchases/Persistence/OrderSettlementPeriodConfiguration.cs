using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class OrderSettlementPeriodConfiguration : IEntityTypeConfiguration<OrderSettlementPeriod>
{
    public void Configure(EntityTypeBuilder<OrderSettlementPeriod> builder)
    {
        builder.ToTable("OrderSettlementPeriods", table =>
        {
            // دورهٔ خالی بسته نمی‌شود (D10).
            table.HasCheckConstraint("CK_OrderSettlementPeriods_OrderCount", "\"OrderCount\" >= 1");
            table.HasCheckConstraint("CK_OrderSettlementPeriods_NetShares",
                "\"TotalRegularNetAmountInNoor\" >= 0 AND \"TotalGiftNetAmountInNoor\" >= 0 AND " +
                "\"TotalNetAmountInNoor\" = \"TotalRegularNetAmountInNoor\" + \"TotalGiftNetAmountInNoor\"");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new OrderSettlementPeriodId(value))
            .IsRequired();

        builder.Property(x => x.ClosedByUserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Title).HasMaxLength(OrderSettlementPeriod.TitleMaxLength);
        builder.Property(x => x.Note).HasMaxLength(OrderSettlementPeriod.NoteMaxLength);
        builder.Property(x => x.OrderCount).IsRequired();
        builder.Property(x => x.TotalRegularNetAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.TotalGiftNetAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.TotalNetAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
