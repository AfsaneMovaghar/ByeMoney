using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class ClosedParcelRefundConfiguration : IEntityTypeConfiguration<ClosedParcelRefund>
{
    public void Configure(EntityTypeBuilder<ClosedParcelRefund> builder)
    {
        builder.ToTable("ClosedParcelRefunds", table =>
        {
            table.HasCheckConstraint("CK_ClosedParcelRefunds_NonNegativeRefunds",
                "\"RegularRefundInNoor\" >= 0 AND \"GiftRefundInNoor\" >= 0 AND " +
                "\"RegularRefundInNoor\" + \"GiftRefundInNoor\" > 0");
            table.HasCheckConstraint("CK_ClosedParcelRefunds_ScopeId", FinancialScope.Check());
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new ClosedParcelRefundId(value))
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

        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.PurchaseId).IsUnique();
        builder.HasIndex(x => x.CartId);

        builder.Property(x => x.ScopeId).HasMaxLength(FinancialScope.MaxLength).IsRequired();
        builder.Property(x => x.RegularRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.GiftRefundInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(ClosedParcelRefund.ReasonMaxLength).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}
