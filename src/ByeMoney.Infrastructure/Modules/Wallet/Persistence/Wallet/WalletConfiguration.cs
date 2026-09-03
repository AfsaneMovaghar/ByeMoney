using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Wallet;

using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

public class WalletConfiguration : IEntityTypeConfiguration<WalletEntity>
{
    public void Configure(EntityTypeBuilder<WalletEntity> builder)
    {
        builder.ToTable("Wallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasConversion(
                id => id.Value,
                value => new WalletId(value));

        builder.Property(w => w.AccountId)
            .HasConversion(
                id => id.Value,
                value => new AccountId(value))
            .IsRequired();

        builder.Property(w => w.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(w => w.Balance)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(w => w.LastUpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(w => w.AccountId)
            .IsUnique();

        builder.HasIndex(w => w.UserId)
            .IsUnique();
    }
}

