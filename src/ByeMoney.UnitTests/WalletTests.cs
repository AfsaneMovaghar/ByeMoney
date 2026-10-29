using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Resources;
using FluentAssertions;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class WalletTests
{
    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        var accountId = AccountId.New();
        var userId = UserId.New();

        var wallet = WalletEntity.Create(accountId, userId);

        wallet.Id.Value.Should().NotBeEmpty();
        wallet.AccountId.Should().Be(accountId);
        wallet.UserId.Should().Be(userId);
        wallet.WithdrawableBalance.Should().Be(0m);
        wallet.GiftBalance.Should().Be(0m);
        wallet.TotalBalance.Should().Be(0m);
        wallet.LastUpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Credit_ShouldIncreaseRegularBalance()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());

        var result = wallet.Credit(1500m, LedgerBalanceType.Regular);

        result.RegularCredited.Should().Be(1500m);
        result.GiftCredited.Should().Be(0m);
        wallet.WithdrawableBalance.Should().Be(1500m);
        wallet.TotalBalance.Should().Be(1500m);
    }

    [Fact]
    public void Credit_ShouldIncreaseGiftBalance()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());

        var result = wallet.Credit(500m, LedgerBalanceType.Gift);

        result.GiftCredited.Should().Be(500m);
        result.RegularCredited.Should().Be(0m);
        wallet.GiftBalance.Should().Be(500m);
        wallet.WithdrawableBalance.Should().Be(0m);
        wallet.TotalBalance.Should().Be(500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Credit_ShouldThrowDomainException_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());

        var act = () => wallet.Credit(invalidAmount, LedgerBalanceType.Regular);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.Wallet_CreditMustBeGreaterThanZero);
    }

    [Fact]
    public void Debit_Purchase_ShouldPrioritizeGiftBalanceFirst()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.Credit(100m, LedgerBalanceType.Gift);
        wallet.Credit(200m, LedgerBalanceType.Regular);

        // خرید ۱۵۰ نور: باید ۱۰۰ نور از هدیه و ۵۰ نور از عادی کسر شود
        var result = wallet.Debit(150m, WalletDebitPurpose.Purchase);

        result.GiftDebited.Should().Be(100m);
        result.RegularDebited.Should().Be(50m);
        wallet.GiftBalance.Should().Be(0m);
        wallet.WithdrawableBalance.Should().Be(150m);
        wallet.TotalBalance.Should().Be(150m);
    }

    [Fact]
    public void Debit_Purchase_ShouldDeductOnlyFromGift_WhenGiftIsSufficient()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.Credit(100m, LedgerBalanceType.Gift);
        wallet.Credit(50m, LedgerBalanceType.Regular);

        var result = wallet.Debit(60m, WalletDebitPurpose.Purchase);

        result.GiftDebited.Should().Be(60m);
        result.RegularDebited.Should().Be(0m);
        wallet.GiftBalance.Should().Be(40m);
        wallet.WithdrawableBalance.Should().Be(50m);
        wallet.TotalBalance.Should().Be(90m);
    }

    [Fact]
    public void Debit_WithdrawalOrTransfer_ShouldOnlyDeductFromRegularBalance_AndNeverTouchGift()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.Credit(500m, LedgerBalanceType.Gift);
        wallet.Credit(300m, LedgerBalanceType.Regular);

        var result = wallet.Debit(200m, WalletDebitPurpose.WithdrawalOrTransfer);

        result.GiftDebited.Should().Be(0m);
        result.RegularDebited.Should().Be(200m);
        wallet.GiftBalance.Should().Be(500m);
        wallet.WithdrawableBalance.Should().Be(100m);
        wallet.TotalBalance.Should().Be(600m);
    }

    [Fact]
    public void Debit_WithdrawalOrTransfer_ShouldThrow_WhenRegularBalanceInsufficient_EvenIfGiftAvailable()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.Credit(1000m, LedgerBalanceType.Gift);
        wallet.Credit(50m, LedgerBalanceType.Regular);

        // تلاش برای خروج ۱۰۰ نور، در حالی که عادی فقط ۵۰ نور است: باید خطا دهد
        var act = () => wallet.Debit(100m, WalletDebitPurpose.WithdrawalOrTransfer);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.Wallet_InsufficientBalance);
    }

    [Fact]
    public void Debit_Purchase_ShouldThrow_WhenTotalBalanceInsufficient()
    {
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.Credit(100m, LedgerBalanceType.Gift);
        wallet.Credit(50m, LedgerBalanceType.Regular);

        var act = () => wallet.Debit(200m, WalletDebitPurpose.Purchase);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.Wallet_InsufficientBalance);
    }
}

