using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;
using ByeMoney.Application.Modules.Wallet.Queries.GetWalletTransactions;
using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByeMoney.UnitTests;

/// <summary>
/// گردش کیف پول کاربر (WALLET-HISTORY): فقط Ledger خود کاربر، جمع گردش برابر موجودی،
/// سهم هدیهٔ هر تراکنش و صفحه‌بندی.
/// </summary>
public sealed class WalletTransactionsPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "wallet_tx_test_" + Guid.NewGuid().ToString("N");
    private readonly User _buyer = User.CreateInitial("wallet-tx-buyer-" + Guid.NewGuid().ToString("N"));
    private readonly User _other = User.CreateInitial("wallet-tx-other-" + Guid.NewGuid().ToString("N"));
    private readonly TestCurrentUser _currentUser = new();
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;

    public async Task InitializeAsync()
    {
        var testConnection = Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(testConnection))
        {
            return;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ByeMoney.sln")))
        {
            directory = directory.Parent;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(directory!.FullName, "src", "ByeMoney.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        _adminConnection = testConnection;

        await using (var connection = new NpgsqlConnection(_adminConnection))
        {
            await connection.OpenAsync();
            // نام schema فقط از GUID تولیدشده در همین آزمون است؛ DDL شناسه پارامترپذیر ندارد.
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema, Pooling = false };
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(configuration);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.ConnectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));
        services.AddSingleton<ICurrentUserService>(_currentUser);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Add(_buyer);
        db.Add(_other);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is null)
        {
            return;
        }

        await _provider.DisposeAsync();
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        // حذف فقط schema اختصاصی و تصادفی همین آزمون را هدف می‌گیرد.
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Transactions_SumEqualsBalance_AndGiftShareIsReported()
    {
        var topUpId = Guid.NewGuid().ToString();
        await CreditAsync(_buyer.Id, 100m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, topUpId);
        await CreditAsync(_buyer.Id, 30m, LedgerBalanceType.Gift, LedgerReferenceType.AdminGrant, null);
        var purchaseTransactionId = await DebitPurchaseAsync(_buyer.Id, 50m);

        var page = await TransactionsAsync(_buyer, page: 1, pageSize: 20);
        var balance = await BalanceAsync(_buyer);

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(3);
        page.Items.Sum(x => x.AmountNoor).Should().Be(balance.Balance);
        page.Items.Sum(x => x.GiftAmountNoor).Should().Be(balance.GiftBalance);
        balance.Balance.Should().Be(80m);
        balance.GiftBalance.Should().Be(0m);

        var purchase = page.Items.Single(x => x.TransactionId == purchaseTransactionId);
        purchase.Type.Should().Be(nameof(LedgerReferenceType.Purchase));
        purchase.AmountNoor.Should().Be(-50m);
        purchase.GiftAmountNoor.Should().Be(-30m);

        var topUp = page.Items.Single(x => x.Type == nameof(LedgerReferenceType.TopUp));
        topUp.AmountNoor.Should().Be(100m);
        topUp.GiftAmountNoor.Should().Be(0m);
        topUp.TopUpId.Should().Be(topUpId);
        topUp.OrderId.Should().BeNull();
    }

    [PostgresFact]
    public async Task Transactions_NeverIncludeAnotherUsersLedger()
    {
        await CreditAsync(_buyer.Id, 10m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, Guid.NewGuid().ToString());
        await CreditAsync(_other.Id, 999m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, Guid.NewGuid().ToString());

        var buyerPage = await TransactionsAsync(_buyer, page: 1, pageSize: 20);
        var otherPage = await TransactionsAsync(_other, page: 1, pageSize: 20);

        buyerPage.Items.Should().ContainSingle().Which.AmountNoor.Should().Be(10m);
        otherPage.Items.Should().ContainSingle().Which.AmountNoor.Should().Be(999m);
        buyerPage.Items.Select(x => x.TransactionId)
            .Should().NotIntersectWith(otherPage.Items.Select(x => x.TransactionId));
    }

    [PostgresFact]
    public async Task Transactions_UserWithoutWallet_GetsEmptyPage()
    {
        var page = await TransactionsAsync(_buyer, page: 1, pageSize: 20);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [PostgresFact]
    public async Task Transactions_ArePagedWithoutOverlap()
    {
        for (var i = 1; i <= 3; i++)
        {
            await CreditAsync(_buyer.Id, i, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, Guid.NewGuid().ToString());
        }

        var first = await TransactionsAsync(_buyer, page: 1, pageSize: 2);
        var second = await TransactionsAsync(_buyer, page: 2, pageSize: 2);

        first.TotalCount.Should().Be(3);
        first.Items.Should().HaveCount(2);
        second.Items.Should().HaveCount(1);
        first.Items.Concat(second.Items).Select(x => x.TransactionId).Should().OnlyHaveUniqueItems();
        first.Items.Concat(second.Items).Sum(x => x.AmountNoor).Should().Be(6m);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validator_RejectsInvalidPaging(int page, int pageSize)
    {
        var result = new GetWalletTransactionsQueryValidator()
            .Validate(new GetWalletTransactionsQuery(page, pageSize));

        result.IsValid.Should().BeFalse();
    }

    [PostgresFact]
    public async Task Transactions_ResolveTopUpPaymentMethod_AndNullForNonTopUpOrMissing()
    {
        var gatewayTopUp = TopUpRequest.CreateGateway(_buyer.Id, 40m, 10000m, FinancialScope.TarheElahi);
        var assistedTopUp = TopUpRequest.CreateAdminCardToCard(
            _buyer.Id, _other.Id, 20000m, 10000m, null, "idem-" + Guid.NewGuid().ToString("N"), FinancialScope.TarheElahi);
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(gatewayTopUp, assistedTopUp);
            await db.SaveChangesAsync();
        }

        var missingTopUpId = Guid.NewGuid().ToString();
        await CreditAsync(_buyer.Id, 40m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, gatewayTopUp.Id.ToString());
        await CreditAsync(_buyer.Id, 20m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, assistedTopUp.Id.ToString());
        await CreditAsync(_buyer.Id, 10m, LedgerBalanceType.Regular, LedgerReferenceType.TopUp, missingTopUpId);
        await CreditAsync(_buyer.Id, 15m, LedgerBalanceType.Gift, LedgerReferenceType.AdminGrant, null);

        var page = await TransactionsAsync(_buyer, page: 1, pageSize: 20);

        page.Items.Should().HaveCount(4);
        page.Items.Single(x => x.TopUpId == gatewayTopUp.Id.ToString()).PaymentMethod.Should().Be("Gateway");
        page.Items.Single(x => x.TopUpId == assistedTopUp.Id.ToString()).PaymentMethod.Should().Be("CardToCard");
        page.Items.Single(x => x.TopUpId == missingTopUpId).PaymentMethod.Should().BeNull();
        page.Items.Single(x => x.Type == nameof(LedgerReferenceType.AdminGrant)).PaymentMethod.Should().BeNull();
    }

    [PostgresFact]
    public async Task Transactions_PageWithoutTopUps_SucceedsWithNullPaymentMethod()
    {
        await CreditAsync(_buyer.Id, 25m, LedgerBalanceType.Gift, LedgerReferenceType.AdminGrant, null);
        await DebitPurchaseAsync(_buyer.Id, 10m);

        var page = await TransactionsAsync(_buyer, page: 1, pageSize: 20);

        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(x => x.PaymentMethod == null);
    }

    private async Task CreditAsync(
        UserId userId,
        decimal amount,
        LedgerBalanceType balanceType,
        LedgerReferenceType referenceType,
        string? referenceId)
    {
        using var scope = _provider.CreateScope();
        var credit = scope.ServiceProvider.GetRequiredService<INoorCreditService>();
        await credit.CreditAsync(new NoorCreditRequest(FinancialScope.TarheElahi, userId, amount, balanceType, referenceType, referenceId));
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
    }

    private async Task<Guid> DebitPurchaseAsync(UserId userId, decimal amount)
    {
        using var scope = _provider.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        var posting = scope.ServiceProvider.GetRequiredService<IWalletPostingService>();
        var wallet = await provisioning.GetOrCreateUserWalletAsync(userId, FinancialScope.TarheElahi);
        var system = await provisioning.GetOrCreateSystemAccountAsync(FinancialScope.TarheElahi);
        var transactionId = Guid.NewGuid();

        await posting.PostDebitAsync(
            wallet, system, amount, WalletDebitPurpose.Purchase, transactionId,
            LedgerReferenceType.Purchase, Guid.NewGuid().ToString(), null, default);
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
        return transactionId;
    }

    private async Task<WalletTransactionPage<WalletTransactionItem>> TransactionsAsync(User user, int page, int pageSize)
    {
        _currentUser.UserId = user.Id.Value;
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetWalletTransactionsQuery(page, pageSize));
    }

    private async Task<WalletBalanceResponse> BalanceAsync(User user)
    {
        _currentUser.UserId = user.Id.Value;
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetWalletBalanceQuery());
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; }
    }
}
