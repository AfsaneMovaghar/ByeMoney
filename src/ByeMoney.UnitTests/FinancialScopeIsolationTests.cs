using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;
using ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Resources;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class FinancialScopeIsolationTests
{
    [Fact]
    public async Task MovementsOnTarheElahiLeaveTheOtherWalletUntouched()
    {
        var userId = UserId.New();
        var tarhe = WalletFor(userId, FinancialScope.TarheElahi, 100m);
        var other = WalletFor(userId, FinancialScope.BuyMoney, 100m);
        var credit = CreditService(tarhe, other);

        await credit.CreditAsync(new NoorCreditRequest(
            FinancialScope.TarheElahi, userId, 40m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp));
        await credit.CreditAsync(new NoorCreditRequest(
            FinancialScope.TarheElahi, userId, 15m, LedgerBalanceType.Gift, LedgerReferenceType.WelcomeGift));

        var posting = Posting();
        await posting.PostDebitAsync(
            tarhe, SystemFor(FinancialScope.TarheElahi), 25m, WalletDebitPurpose.WithdrawalOrTransfer,
            Guid.NewGuid(), LedgerReferenceType.Withdrawal, "withdrawal", null, CancellationToken.None);

        tarhe.Wallet.TotalBalance.Should().Be(130m);
        tarhe.Wallet.WithdrawableBalance.Should().Be(115m);
        other.Wallet.TotalBalance.Should().Be(100m);

        await credit.CreditAsync(new NoorCreditRequest(
            FinancialScope.BuyMoney, userId, 10m, LedgerBalanceType.Regular, LedgerReferenceType.Refund));

        tarhe.Wallet.TotalBalance.Should().Be(130m);
        other.Wallet.TotalBalance.Should().Be(110m);
    }

    [Fact]
    public void TarheElahiCartPaymentDoesNotDebitTheOtherWallet()
    {
        var buyer = UserId.New();
        var tarhe = WalletFor(buyer, FinancialScope.TarheElahi, 100m);
        var other = WalletFor(buyer, FinancialScope.BuyMoney, 80m);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["course"], null, FinancialScope.TarheElahi);
        cart.SetQuote([new CartQuoteItem("course", "دوره", 30, 0, 0, 30)]);

        CartPayment.Apply(cart, tarhe, SystemFor(FinancialScope.TarheElahi));

        tarhe.Wallet.WithdrawableBalance.Should().Be(70m);
        other.Wallet.TotalBalance.Should().Be(80m);
        cart.Status.Should().Be(CartStatus.Paid);
    }

    [Fact]
    public void CrossScopeLedgerPairIsRejectedBeforeAnyBalanceChanges()
    {
        var buyer = UserId.New();
        var tarhe = WalletFor(buyer, FinancialScope.TarheElahi, 50m);
        var other = WalletFor(buyer, FinancialScope.BuyMoney, 40m);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["course"], null, FinancialScope.TarheElahi);
        cart.SetQuote([new CartQuoteItem("course", "دوره", 20, 0, 0, 20)]);

        var act = () => CartPayment.Apply(cart, other, SystemFor(FinancialScope.BuyMoney));

        act.Should().Throw<DomainException>().WithMessage(DomainErrors.Ledger_AccountsMustShareScope);
        tarhe.Wallet.TotalBalance.Should().Be(50m);
        other.Wallet.TotalBalance.Should().Be(40m);
        cart.Status.Should().NotBe(CartStatus.Paid);
    }

    [Fact]
    public async Task BalanceBatchAndSettlementPreviewSeeOnlyTarheElahi()
    {
        var userId = UserId.New();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.UserId).Returns(userId.Value);
        var wallets = new Mock<IWalletRepository>();
        var tarhe = WalletFor(userId, FinancialScope.TarheElahi, 12m);
        wallets.Setup(x => x.GetByUserIdAsync(userId, FinancialScope.TarheElahi, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tarhe.Wallet);
        wallets.Setup(x => x.GetByUserIdAsync(userId, FinancialScope.BuyMoney, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WalletFor(userId, FinancialScope.BuyMoney, 99m).Wallet);

        var balance = await new GetWalletBalanceQueryHandler(wallets.Object, current.Object)
            .Handle(new GetWalletBalanceQuery(), CancellationToken.None);
        balance.Balance.Should().Be(12m);

        wallets.Setup(x => x.GetBalancesAndGiftBalancesByExternalUserIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), FinancialScope.TarheElahi, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Dictionary<string, decimal> { ["u"] = 12m }, new Dictionary<string, decimal> { ["u"] = 1m }));
        wallets.Setup(x => x.GetBalancesAndGiftBalancesByExternalUserIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), FinancialScope.BuyMoney, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Dictionary<string, decimal> { ["u"] = 99m }, new Dictionary<string, decimal> { ["u"] = 99m }));

        var batch = await new GetBatchWalletBalancesQueryHandler(wallets.Object)
            .Handle(new GetBatchWalletBalancesQuery(["u"]), CancellationToken.None);
        batch.Balances["u"].Should().Be(12m);
        batch.GiftBalances["u"].Should().Be(1m);

        var reader = new Mock<IOrderReader>();
        reader.Setup(x => x.ReadAsync(null, null, FinancialScope.TarheElahi, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptySnapshot);
        reader.Setup(x => x.ReadAsync(null, null, FinancialScope.BuyMoney, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("other scope was read"));
        var store = new Mock<IOrderSettlementStore>();
        store.Setup(x => x.GetSettledOrderIdsAsync(FinancialScope.TarheElahi, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        store.Setup(x => x.GetShippingPaymentsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PaymentShares>());
        var preview = await new GetOrderSettlementPreviewQueryHandler(
                new OrderSettlementCandidateService(reader.Object, store.Object), current.Object)
            .Handle(new GetOrderSettlementPreviewQuery(), CancellationToken.None);

        preview.OrderCount.Should().Be(0);
        preview.TotalNetAmountInNoor.Should().Be(0m);
        reader.Verify(x => x.ReadAsync(null, null, FinancialScope.TarheElahi, It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(x => x.ReadAsync(null, null, FinancialScope.BuyMoney, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly OrderSnapshot EmptySnapshot = new([], [], [], [], [], []);

    private static ProvisionedUserWallet WalletFor(UserId userId, string scopeId, decimal balance)
    {
        var account = Account.CreateUserAccount(userId, scopeId);
        var wallet = Wallet.Create(account.Id, userId, scopeId);
        wallet.ApplyCredit(balance);
        return new ProvisionedUserWallet(account, wallet);
    }

    private static Account SystemFor(string scopeId) =>
        scopeId == FinancialScope.TarheElahi
            ? Account.CreateSystemAccount(scopeId)
            : Account.CreateSystemAccount(scopeId, AccountId.New());

    private static WalletPostingService Posting()
    {
        var ledger = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        ledger.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return new WalletPostingService(ledger.Object, new Mock<IWalletRepository>().Object);
    }

    private static NoorCreditService CreditService(ProvisionedUserWallet tarhe, ProvisionedUserWallet other)
    {
        var provisioning = new Mock<IUserWalletProvisioningService>();
        provisioning.Setup(x => x.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), FinancialScope.TarheElahi, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tarhe);
        provisioning.Setup(x => x.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), FinancialScope.BuyMoney, It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);
        provisioning.Setup(x => x.GetOrCreateSystemAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string scopeId, CancellationToken _) => SystemFor(scopeId));
        return new NoorCreditService(provisioning.Object, Posting());
    }
}
