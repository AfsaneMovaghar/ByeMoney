using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
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
            table.HasCheckConstraint("CK_PhysicalProductPurchases_Quantity", "\"Quantity\" >= 1");
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
        builder.HasIndex(x => x.CartId);
        builder.HasIndex(x => x.BuyerId);

        builder.Property(x => x.ExternalProductId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductTitle).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPriceInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.PaidAmountInNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.LedgerTransactionId);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}
