using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class GetBatchWalletBalancesQueryTests
{
    [Fact]
    public async Task Handle_ReturnsStoredBalancesAndZeroForMissingWallets()
    {
        var wallets = new Mock<IWalletRepository>();
        wallets.Setup(x => x.GetBalancesByExternalUserIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { ["user-1"] = 150.25m, ["user-3"] = 500m });
        var handler = new GetBatchWalletBalancesQueryHandler(wallets.Object);

        var response = await handler.Handle(
            new GetBatchWalletBalancesQuery(["user-1", "user-2", "user-3", "user-1"]),
            CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["user-1"] = 150.25m,
            ["user-2"] = 0m,
            ["user-3"] = 500m
        });
        wallets.Verify(x => x.GetBalancesByExternalUserIdsAsync(
            It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_RejectsEmptyAndOversizedBatches()
    {
        var validator = new GetBatchWalletBalancesQueryValidator();

        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery(null!))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery([]))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery(Enumerable.Repeat("id", 501).ToArray()))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery([" "]))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery([new string('a', 101)]))).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new GetBatchWalletBalancesQuery(["valid-user-id"]))).IsValid.Should().BeTrue();
    }
}
