using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class GetWalletBalanceQueryHandlerTests
{
    private readonly Mock<IWalletRepository> _walletRepositoryMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly GetWalletBalanceQueryHandler _handler;

    public GetWalletBalanceQueryHandlerTests()
    {
        _walletRepositoryMock = new Mock<IWalletRepository>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _handler = new GetWalletBalanceQueryHandler(
            _walletRepositoryMock.Object,
            _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectBalance_WhenUserHasExistingWalletWithNonzeroBalance()
    {
        // Arrange
        var userId = UserId.New();
        var accountId = AccountId.New();
        var wallet = WalletEntity.Create(accountId, userId);
        wallet.ApplyCredit(2500m);

        _currentUserServiceMock
            .Setup(s => s.UserId)
            .Returns(userId.Value);

        _walletRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var query = new GetWalletBalanceQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Balance.Should().Be(2500m);
        result.Currency.Should().Be("Noor");
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroBalance_AndNotCreateWallet_WhenUserHasNoWalletYet()
    {
        // Arrange
        var userId = UserId.New();

        _currentUserServiceMock
            .Setup(s => s.UserId)
            .Returns(userId.Value);

        _walletRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletEntity?)null);

        var query = new GetWalletBalanceQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Balance.Should().Be(0m);
        result.Currency.Should().Be("Noor");

        // Verify that no wallet was created or added to repository as a side effect
        _walletRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WalletEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _currentUserServiceMock
            .Setup(s => s.UserId)
            .Returns((Guid?)null);

        var query = new GetWalletBalanceQuery();

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

