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

        builder.Property(t => t.CreatedByUserId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, value => value.HasValue ? new UserId(value.Value) : null);

        builder.Property(t => t.ChargeType).HasConversion<string>().HasMaxLength(80);
        builder.Property(t => t.AmountRial).HasPrecision(18, 2);
        builder.Property(t => t.RialPerNoorSnapshot).HasPrecision(18, 8);
        builder.Property(t => t.ReceiptId).HasMaxLength(200);
        builder.Property(t => t.IdempotencyKey).HasMaxLength(100);
        builder.HasIndex(t => t.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.Property(t => t.AmountNoor)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(t => t.ClientReferenceCode)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.ExternalTransactionId)
            .HasMaxLength(100);

        builder.Property(t => t.GatewayName).HasMaxLength(30);
        builder.Property(t => t.BankReferenceNumber).HasMaxLength(100);
        builder.Property(t => t.GatewayEventId).HasMaxLength(100).IsConcurrencyToken();
        builder.Property(t => t.GatewayResultKind).HasMaxLength(30);
        builder.Property(t => t.GatewayResultCode).HasMaxLength(100);
        builder.Property(t => t.GatewayOriginalAmountRial).HasPrecision(18, 2);
        builder.Property(t => t.GatewayAffectiveAmountRial).HasPrecision(18, 2);
        builder.Property(t => t.ReviewCaseId).HasMaxLength(100).IsConcurrencyToken();
        builder.HasIndex(t => t.ReviewCaseId).IsUnique().HasFilter("\"ReviewCaseId\" IS NOT NULL");
        builder.Property(t => t.ReviewReasonCode).HasMaxLength(50);
        builder.Property(t => t.ReviewOutcomeCode).HasMaxLength(50);
        builder.Property(t => t.ReviewResolvedAtUtc).IsConcurrencyToken();
        builder.Property(t => t.ReviewResolutionFinancialReferenceId).HasMaxLength(100);
        builder.Property(t => t.ReviewRevision).IsConcurrencyToken();
        builder.Property(t => t.ManualRefundReference).HasMaxLength(100).IsConcurrencyToken();
        builder.Property(t => t.ReviewAudit).HasConversion(
            value => System.Text.Json.JsonSerializer.Serialize(value, (System.Text.Json.JsonSerializerOptions?)null),
            value => System.Text.Json.JsonSerializer.Deserialize<List<FinancialReviewAudit>>(value,
                (System.Text.Json.JsonSerializerOptions?)null) ?? new List<FinancialReviewAudit>()).HasColumnType("jsonb");

        builder.Property(t => t.RejectionReason)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.PendingItems)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<PendingItemSnapshot>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<PendingItemSnapshot>())
            .HasColumnType("jsonb");

        builder.HasIndex(t => new { t.UserId, t.Status });

        builder.HasIndex(t => t.ExternalTransactionId)
            .IsUnique()
            .HasFilter("\"PaymentMethod\" = 'Gateway' AND \"ExternalTransactionId\" IS NOT NULL");

        builder.HasIndex(t => t.ClientReferenceCode)
            .IsUnique();
    }
}

