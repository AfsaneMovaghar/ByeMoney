using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Wallet.Wallets;

public class Wallet : BaseEntity<WalletId>
{
    public AccountId AccountId { get; private set; }
    public UserId UserId { get; private set; }
    internal decimal RegularBalance { get; private set; }
    public decimal GiftBalance { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }

    public decimal WithdrawableBalance => RegularBalance;
    public decimal TotalBalance => checked(RegularBalance + GiftBalance);

    private Wallet() { }

    public static Wallet Create(AccountId accountId, UserId userId)
    {
        return new Wallet
        {
            Id = WalletId.New(),
            AccountId = accountId,
            UserId = userId,
            RegularBalance = 0m,
            GiftBalance = 0m,
            LastUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public WalletDebitResult Debit(decimal amount, WalletDebitPurpose purpose)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.Wallet_DebitMustBeGreaterThanZero);

        decimal giftDebited;
        decimal regularDebited;

        if (purpose == WalletDebitPurpose.Purchase)
        {
            if (TotalBalance < amount)
                throw new DomainException(DomainErrors.Wallet_InsufficientBalance);

            giftDebited = Math.Min(GiftBalance, amount);
            regularDebited = amount - giftDebited;
        }
        else if (purpose == WalletDebitPurpose.WithdrawalOrTransfer)
        {
            if (WithdrawableBalance < amount)
                throw new DomainException(DomainErrors.Wallet_InsufficientBalance);

            giftDebited = 0m;
            regularDebited = amount;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        GiftBalance -= giftDebited;
        RegularBalance -= regularDebited;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return new WalletDebitResult(giftDebited, regularDebited);
    }

    public WalletCreditResult Credit(decimal amount, LedgerBalanceType balanceType)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.Wallet_CreditMustBeGreaterThanZero);

        decimal giftCredited = 0m;
        decimal regularCredited = 0m;

        if (balanceType == LedgerBalanceType.Gift)
        {
            GiftBalance += amount;
            giftCredited = amount;
        }
        else if (balanceType == LedgerBalanceType.Regular)
        {
            RegularBalance += amount;
            regularCredited = amount;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(balanceType));
        }

        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return new WalletCreditResult(giftCredited, regularCredited);
    }

    internal void ApplyConcurrencyDelta(decimal regularDelta, decimal giftDelta)
    {
        var newRegular = RegularBalance + regularDelta;
        var newGift = GiftBalance + giftDelta;

        if (newRegular < 0 || newGift < 0)
        {
            throw new DomainException(DomainErrors.Wallet_InsufficientBalance);
        }

        RegularBalance = newRegular;
        GiftBalance = newGift;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

