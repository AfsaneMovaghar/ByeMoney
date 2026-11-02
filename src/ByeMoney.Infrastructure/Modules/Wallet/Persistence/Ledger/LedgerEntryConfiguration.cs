using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using Microsoft.EntityFrameworkCore;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Ledger;

using AccountEntity = ByeMoney.Domain.Modules.Wallet.Accounts.Account;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint(
                "CK_LedgerEntries_Amount_NonZero",
                "\"Amount\" <> 0");
            table.AddEnumCheckConstraint<LedgerEntry, LedgerReferenceType>(
                "CK_LedgerEntries_ReferenceType",
                nameof(LedgerEntry.ReferenceType));
            table.AddEnumCheckConstraint<LedgerEntry, LedgerBalanceType>(
                "CK_LedgerEntries_BalanceType",
                nameof(LedgerEntry.BalanceType));
        });

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(
                id => id.Value,
                value => new LedgerEntryId(value));

        builder.Property(l => l.AccountId)
            .HasConversion(
                id => id.Value,
                value => new AccountId(value))
            .IsRequired();

        builder.Property(l => l.PerformedByUserId)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, value => value.HasValue ? new UserId(value.Value) : null);
        builder.Property(l => l.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(l => l.TransactionId)
            .IsRequired();

        builder.Property(l => l.ReferenceId)
            .HasMaxLength(100);

        builder.Property(l => l.CreatedAtUtc)
            .IsRequired();

        builder.Property(l => l.ReferenceType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.BalanceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(LedgerBalanceType.Regular)
            .IsRequired();

        builder.HasOne<AccountEntity>()
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.TransactionId);

        builder.HasIndex(l => new { l.ReferenceType, l.ReferenceId });

        builder.HasIndex(l => new { l.AccountId, l.CreatedAtUtc });
    }
}

