using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Ledger;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");

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
            .IsRequired();

        builder.HasIndex(l => l.TransactionId);

        builder.HasIndex(l => l.ReferenceType);

        builder.HasIndex(l => new { l.AccountId, l.CreatedAtUtc });
    }
}

