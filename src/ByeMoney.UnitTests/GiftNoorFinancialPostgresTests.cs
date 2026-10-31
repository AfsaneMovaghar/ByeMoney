using ByeMoney.Application;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class GiftNoorFinancialPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "gift_fin_test_" + Guid.NewGuid().ToString("N");
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;
    private readonly User _buyer = User.CreateInitial("gift-test-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();

    private ICartStore Store => _provider.GetRequiredService<ICartStore>();

    public async Task InitializeAsync()
    {
        var testConn = Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(testConn) && Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1")
            return;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ByeMoney.sln")))
            directory = directory.Parent;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(directory!.FullName, "src", "ByeMoney.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _adminConnection = !string.IsNullOrWhiteSpace(testConn)
            ? testConn
            : configuration.GetConnectionString("DefaultConnection")!;

        await using (var connection = new NpgsqlConnection(_adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema };
        var services = new ServiceCollection().AddLogging().AddApplicationServices().AddInfrastructureServices(configuration);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.ConnectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));
        services.AddSingleton(_pricing.Object);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Add(_buyer);
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        await provisioning.GetOrCreateUserWalletAsync(_buyer.Id, default);
        await provisioning.GetOrCreateSystemAccountAsync(default);
        await db.SaveChangesAsync();

        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ByeMoney.Domain.Common.Result.Success());
    }

    public async Task DisposeAsync()
    {
        if (_provider is null) return;
        await _provider.DisposeAsync();
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedWalletAsync(decimal regular, decimal gift)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        var account = await db.Set<ByeMoney.Domain.Modules.Wallet.Accounts.Account>().SingleAsync(x => x.Id == wallet.AccountId);
        var systemAccount = await db.Set<ByeMoney.Domain.Modules.Wallet.Accounts.Account>().SingleAsync(x => x.Type == ByeMoney.Domain.Modules.Wallet.Accounts.AccountType.System);

        if (regular > 0)
        {
            wallet.Credit(regular, LedgerBalanceType.Regular);
            var txId = Guid.NewGuid();
            db.Add(LedgerEntry.Create(account.Id, regular, txId, LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));
            db.Add(LedgerEntry.Create(systemAccount.Id, -regular, txId, LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));
        }

        if (gift > 0)
        {
            wallet.Credit(gift, LedgerBalanceType.Gift);
            var txId = Guid.NewGuid();
            db.Add(LedgerEntry.Create(account.Id, gift, txId, LedgerReferenceType.AdminGrant, balanceType: LedgerBalanceType.Gift));
            db.Add(LedgerEntry.Create(systemAccount.Id, -gift, txId, LedgerReferenceType.AdminGrant, balanceType: LedgerBalanceType.Gift));
        }

        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Purchase_WithOnlyGiftBalance_DebitsGiftAndWritesGiftLedgerEntries()
    {
        await SeedWalletAsync(regular: 0m, gift: 100m);

        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["c-1"], null);
        cart.SetQuote([new("c-1", "دوره ۱", 60m, 0m, 0m, 60m)]);
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var payResult = await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default);
        payResult.IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        wallet.GiftBalance.Should().Be(40m);
        wallet.WithdrawableBalance.Should().Be(0m);
        wallet.TotalBalance.Should().Be(40m);

        var entries = await db.Set<LedgerEntry>().Where(x => x.ReferenceId == cart.Id.ToString()).ToListAsync();
        entries.Should().HaveCount(2); // ۱ جفت هدیه
        entries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Gift);
        entries.Single(x => x.AccountId == wallet.AccountId).Amount.Should().Be(-60m);
    }

    [PostgresFact]
    public async Task Purchase_WithGiftAndRegular_PrioritizesGiftFirstAndWritesBothLedgerPairs()
    {
        await SeedWalletAsync(regular: 100m, gift: 40m);

        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["c-1"], null);
        cart.SetQuote([new("c-1", "دوره ۱", 70m, 0m, 0m, 70m)]);
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var payResult = await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default);
        payResult.IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);

        // هدیه اول ۴۰ تا کسر و صفر شده؛ مابقی ۳۰ تا از عادی کسر شده و ۷۰ مانده
        wallet.GiftBalance.Should().Be(0m);
        wallet.WithdrawableBalance.Should().Be(70m);
        wallet.TotalBalance.Should().Be(70m);

        var purchaseEntries = await db.Set<LedgerEntry>().Where(x => x.ReferenceId == cart.Id.ToString()).ToListAsync();
        purchaseEntries.Should().HaveCount(4); // ۲ جفت: هدیه و عادی

        var userGiftEntry = purchaseEntries.Single(x => x.AccountId == wallet.AccountId && x.BalanceType == LedgerBalanceType.Gift);
        userGiftEntry.Amount.Should().Be(-40m);

        var userRegularEntry = purchaseEntries.Single(x => x.AccountId == wallet.AccountId && x.BalanceType == LedgerBalanceType.Regular);
        userRegularEntry.Amount.Should().Be(-30m);

        // تطبیق جبری کل Ledger با مانده والت
        var userEntries = await db.Set<LedgerEntry>().Where(x => x.AccountId == wallet.AccountId).ToListAsync();
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Gift).Sum(x => x.Amount).Should().Be(wallet.GiftBalance);
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Regular).Sum(x => x.Amount).Should().Be(wallet.WithdrawableBalance);
        userEntries.Sum(x => x.Amount).Should().Be(wallet.TotalBalance);
    }

    [PostgresFact]
    public async Task FreeCart_DoesNotDebitWalletOrWriteLedger()
    {
        await SeedWalletAsync(regular: 50m, gift: 50m);

        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["free-c"], null);
        cart.SetQuote([new("free-c", "دوره رایگان", 0m, 0m, 0m, 0m)]);
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var payResult = await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default);
        payResult.IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        wallet.GiftBalance.Should().Be(50m);
        wallet.WithdrawableBalance.Should().Be(50m);

        var purchaseEntries = await db.Set<LedgerEntry>().Where(x => x.ReferenceId == cart.Id.ToString()).ToListAsync();
        purchaseEntries.Should().BeEmpty();
    }

    [PostgresFact]
    public async Task Replay_Payment_IsIdempotentAndDoesNotDuplicateDebitOrLedger()
    {
        await SeedWalletAsync(regular: 50m, gift: 50m);

        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["c-1"], null);
        cart.SetQuote([new("c-1", "دوره ۱", 40m, 0m, 0m, 40m)]);
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        wallet.GiftBalance.Should().Be(10m);
        wallet.WithdrawableBalance.Should().Be(50m);

        var purchaseEntries = await db.Set<LedgerEntry>().Where(x => x.ReferenceId == cart.Id.ToString()).ToListAsync();
        purchaseEntries.Should().HaveCount(2); // فقط یکبار کسر شده
    }

    [PostgresFact]
    public async Task ShortfallCalculation_TakesGiftBalanceIntoAccount()
    {
        await SeedWalletAsync(regular: 20m, gift: 30m); // موجودی کل ۵۰ نور

        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["c-1"], null);
        cart.SetQuote([new("c-1", "دوره ۱", 120m, 0m, 0m, 120m)]); // ارزش سبد ۱۲۰ نور -> کسری باید ۷۰ نور باشد
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var topUpResult = await Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default);
        topUpResult.IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var topUp = await db.Set<ByeMoney.Domain.Modules.Wallet.TopUps.TopUpRequest>().FindAsync([new ByeMoney.Domain.Modules.Wallet.TopUps.TopUpRequestId(topUpResult.Value.TopUpRequestId)]);
        topUp!.AmountNoor.Should().Be(70m); // ۱۲۰ منهای ۵۰ موجودی کل
    }
}
