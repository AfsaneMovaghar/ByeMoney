using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.TopUp;

public class TopUpRequestConfiguration : IEntityTypeConfiguration<TopUpRequest>
{
    public void Configure(EntityTypeBuilder<TopUpRequest> builder)
    {
        builder.ToTable("TopUpRequests");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => new TopUpRequestId(value));

        builder.Property(t => t.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.ClientReferenceCode)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.ExternalTransactionId)
            .HasMaxLength(100);

        builder.Property(t => t.RejectionReason)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Status });

        builder.HasIndex(t => t.ExternalTransactionId);

        builder.HasIndex(t => t.ClientReferenceCode)
            .IsUnique();
    }
}

