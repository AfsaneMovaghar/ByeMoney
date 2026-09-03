using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class WalletTests
{
    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        // Arrange
        var accountId = AccountId.New();
        var userId = UserId.New();

        // Act
        var wallet = WalletEntity.Create(accountId, userId);

        // Assert
        wallet.Id.Value.Should().NotBeEmpty();
        wallet.AccountId.Should().Be(accountId);
        wallet.UserId.Should().Be(userId);
        wallet.Balance.Should().Be(0m);
        wallet.LastUpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ApplyCredit_ShouldIncreaseBalance()
    {
        // Arrange
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());

        // Act
        wallet.ApplyCredit(1500m);

        // Assert
        wallet.Balance.Should().Be(1500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void ApplyCredit_ShouldThrowDomainException_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        // Arrange
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());

        // Act
        var act = () => wallet.ApplyCredit(invalidAmount);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void ApplyDebit_ShouldDecreaseBalance_WhenSufficient()
    {
        // Arrange
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.ApplyCredit(2000m);

        // Act
        wallet.ApplyDebit(800m);

        // Assert
        wallet.Balance.Should().Be(1200m);
    }

    [Fact]
    public void ApplyDebit_ShouldThrowDomainException_WhenInsufficientBalance()
    {
        // Arrange
        var wallet = WalletEntity.Create(AccountId.New(), UserId.New());
        wallet.ApplyCredit(500m);

        // Act
        var act = () => wallet.ApplyDebit(600m);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Insufficient*");
    }
}

