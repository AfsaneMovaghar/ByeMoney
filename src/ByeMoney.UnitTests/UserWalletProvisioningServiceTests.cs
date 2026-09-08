using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class UserWalletProvisioningServiceTests
{
    private readonly Mock<IAccountRepository> _accountRepositoryMock;
    private readonly Mock<IWalletRepository> _walletRepositoryMock;
    private readonly UserWalletProvisioningService _service;

    public UserWalletProvisioningServiceTests()
    {
        _accountRepositoryMock = new Mock<IAccountRepository>();
        _walletRepositoryMock = new Mock<IWalletRepository>();
        _service = new UserWalletProvisioningService(
            _accountRepositoryMock.Object,
            _walletRepositoryMock.Object);
    }

    [Fact]
    public async Task GetOrCreateUserWalletAsync_ShouldReturnExistingEntities_WithoutAdding_WhenBothAlreadyExist()
    {
        // Arrange
        var userId = UserId.New();
        var existingAccount = Account.CreateUserAccount(userId);
        var existingWallet = WalletEntity.Create(existingAccount.Id, userId);

        _accountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        _walletRepositoryMock
            .Setup(r => r.GetByAccountIdAsync(existingAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingWallet);

        // Act
        var result = await _service.GetOrCreateUserWalletAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Account.Should().BeSameAs(existingAccount);
        result.Wallet.Should().BeSameAs(existingWallet);

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _walletRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateUserWalletAsync_ShouldCreateBothAccountAndWallet_WhenNeitherExists()
    {
        // Arrange
        var userId = UserId.New();

        _accountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        _walletRepositoryMock
            .Setup(r => r.GetByAccountIdAsync(It.IsAny<AccountId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletEntity?)null);

        Account? addedAccount = null;
        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((acc, _) => addedAccount = acc)
            .Returns(Task.CompletedTask);

        WalletEntity? addedWallet = null;
        _walletRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WalletEntity, CancellationToken>((w, _) => addedWallet = w)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetOrCreateUserWalletAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Account.Should().NotBeNull();
        result.Wallet.Should().NotBeNull();

        result.Account.UserId.Should().Be(userId);
        result.Account.Type.Should().Be(AccountType.User);
        result.Account.Status.Should().Be(AccountStatus.Active);

        result.Wallet.UserId.Should().Be(userId);
        result.Wallet.AccountId.Should().Be(result.Account.Id);
        result.Wallet.Balance.Should().Be(0m);

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _walletRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateUserWalletAsync_ShouldCreateWallet_WhenAccountExistsButWalletDoesNotExist()
    {
        // Arrange
        var userId = UserId.New();
        var existingAccount = Account.CreateUserAccount(userId);

        _accountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        _walletRepositoryMock
            .Setup(r => r.GetByAccountIdAsync(existingAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletEntity?)null);

        WalletEntity? addedWallet = null;
        _walletRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WalletEntity, CancellationToken>((w, _) => addedWallet = w)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetOrCreateUserWalletAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Account.Should().BeSameAs(existingAccount);
        result.Wallet.Should().NotBeNull();
        result.Wallet.AccountId.Should().Be(existingAccount.Id);
        result.Wallet.UserId.Should().Be(userId);

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _walletRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSystemAccountAsync_ShouldReturnExistingSystemAccount_WhenAlreadyExists()
    {
        // Arrange
        var existingSystemAccount = Account.CreateSystemAccount();

        _accountRepositoryMock
            .Setup(r => r.GetSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSystemAccount);

        // Act
        var result = await _service.GetOrCreateSystemAccountAsync(CancellationToken.None);

        // Assert
        result.Should().BeSameAs(existingSystemAccount);

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateSystemAccountAsync_ShouldCreateSystemAccount_WhenDoesNotExist()
    {
        // Arrange
        _accountRepositoryMock
            .Setup(r => r.GetSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        Account? addedSystemAccount = null;
        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((acc, _) => addedSystemAccount = acc)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetOrCreateSystemAccountAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(AccountType.System);
        result.UserId.Should().BeNull();

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
