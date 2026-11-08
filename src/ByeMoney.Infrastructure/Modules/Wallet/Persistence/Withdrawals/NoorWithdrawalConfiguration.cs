using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Withdrawals;

public class NoorWithdrawalConfiguration : IEntityTypeConfiguration<NoorWithdrawal>
{
    private const int EnumMaxLength = 30;

    public void Configure(EntityTypeBuilder<NoorWithdrawal> builder)
    {
        builder.ToTable("NoorWithdrawals", table =>
        {
            table.AddEnumCheckConstraint<NoorWithdrawal, NoorWithdrawalKind>(
                "CK_NoorWithdrawals_Kind",
                nameof(NoorWithdrawal.Kind));
            table.AddEnumCheckConstraint<NoorWithdrawal, NoorWithdrawalStatus>(
                "CK_NoorWithdrawals_Status",
                nameof(NoorWithdrawal.Status));
            table.HasCheckConstraint(
                "CK_NoorWithdrawals_Amounts_Positive",
                "\"AmountNoor\" > 0 AND \"AmountRial\" > 0");

            // فقط استرداد پرداخت تکراری به شارژ وصل است.
            table.HasCheckConstraint(
                "CK_NoorWithdrawals_RelatedTopUp",
                "(\"Kind\" = 'DuplicatePaymentRefund') = (\"RelatedTopUpId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_NoorWithdrawals_BeneficiaryAccount",
                "\"BeneficiaryCardNumber\" IS NOT NULL OR \"BeneficiaryIban\" IS NOT NULL");

            // هر وضعیت پایانی شواهد خودش را دارد: واریز با مرجع و فیش، جبران با تراکنش برگشت.
            table.HasCheckConstraint(
                "CK_NoorWithdrawals_PaidEvidence",
                "\"Status\" <> 'Paid' OR (\"PaymentReference\" IS NOT NULL AND \"ReceiptId\" IS NOT NULL " +
                "AND \"PaidByUserId\" IS NOT NULL AND \"PaidAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_NoorWithdrawals_ReversalEvidence",
                "\"Status\" <> 'Reversed' OR (\"ReversalReason\" IS NOT NULL AND \"ReversalTransactionId\" IS NOT NULL " +
                "AND \"ReversedByUserId\" IS NOT NULL AND \"ReversedAtUtc\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new NoorWithdrawalId(value))
            .IsRequired();

        builder.Property(x => x.OperationId).IsRequired();
        builder.HasIndex(x => x.OperationId).IsUnique();

        builder.Property(x => x.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();
        builder.Property(x => x.CreatedByUserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();
        builder.Property(x => x.PaidByUserId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);
        builder.Property(x => x.ReversedByUserId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReversedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.RelatedTopUpId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new TopUpRequestId(value.Value) : null);
        builder.HasOne<TopUpRequest>()
            .WithMany()
            .HasForeignKey(x => x.RelatedTopUpId)
            .OnDelete(DeleteBehavior.Restrict);

        // یک شارژ تکراری فقط یک استرداد برنگشته دارد؛ پس از جبران، استرداد دوباره ممکن است.
        builder.HasIndex(x => x.RelatedTopUpId)
            .IsUnique()
            .HasFilter("\"RelatedTopUpId\" IS NOT NULL AND \"Status\" <> 'Reversed'");

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(EnumMaxLength).IsRequired();

        // ثبت واریز و «پرداخت نشد» هم‌زمان فقط یک نتیجه دارند.
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.AmountNoor).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.AmountRial).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.BeneficiaryName).HasMaxLength(NoorWithdrawal.BeneficiaryNameMaxLength).IsRequired();
        builder.Property(x => x.BeneficiaryCardNumber).HasMaxLength(NoorWithdrawal.CardNumberLength);
        builder.Property(x => x.BeneficiaryIban).HasMaxLength(NoorWithdrawal.IbanLength);
        builder.Property(x => x.Description).HasMaxLength(NoorWithdrawal.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.LedgerTransactionId).IsRequired();
        builder.Property(x => x.PaymentReference).HasMaxLength(NoorWithdrawal.PaymentReferenceMaxLength);
        builder.Property(x => x.ReceiptId).HasMaxLength(NoorWithdrawal.ReceiptIdMaxLength);
        builder.Property(x => x.ReversalReason).HasMaxLength(NoorWithdrawal.ReversalReasonMaxLength);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.ReceiptId);
    }
}
