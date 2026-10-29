using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

/// <summary>
/// آزمون‌های مالی مسیر شارژ روی PostgreSQL: افزایش نور فقط یک بار، ایمنی برابر تکرار و هم‌زمانی،
/// تأیید دیرهنگام و تفکیک نور شارژ از نور هدیه.
/// </summary>
public sealed class TopUpNoorCreditPostgresTests : IAsyncLifetime
{
    private const decimal RialPerNoor = 1_000m;
    private const string GatewayName = "SEP";

    private readonly string _schema = "topup_noor_test_" + Guid.NewGuid().ToString("N");
    private readonly User _customer = User.CreateInitial("topup-noor-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider? _provider;
    private string? _adminConnection;

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
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.ConnectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        db.Add(_customer);
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        await provisioning.GetOrCreateUserWalletAsync(_customer.Id, default);
        await provisioning.GetOrCreateSystemAccountAsync(default);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is null)
            return;

        await _provider.DisposeAsync();

        if (string.IsNullOrWhiteSpace(_adminConnection))
            return;

        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task DuplicateGatewayConfirmation_SettlesOnce_AndWritesSingleBalancedPair()
    {
        var topUp = await CreateGatewayTopUpAsync(amountNoor: 100m);

        var first = await ConfirmGatewayAsync(topUp, "bank-transaction-1");
        var second = await ConfirmGatewayAsync(topUp, "bank-transaction-1");

        first.Result.IsSuccess.Should().BeTrue();
        second.Result.IsSuccess.Should().BeTrue();
        second.Idempotent.Should().BeTrue();

        var (regular, gift) = await BalancesAsync();
        regular.Should().Be(100m);
        gift.Should().Be(0m);

        var entries = await TopUpEntriesAsync(topUp);
        entries.Should().HaveCount(2);
        entries.Sum(x => x.Amount).Should().Be(0m);
        entries.Select(x => x.TransactionId).Distinct().Should().ContainSingle();
        entries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Regular);
        entries.Should().OnlyContain(x => x.ReferenceType == LedgerReferenceType.TopUp);
        entries.Single(x => x.Amount > 0).Amount.Should().Be(100m);
        entries.Single(x => x.Amount < 0).Amount.Should().Be(-100m);

        var reloaded = await ReloadTopUpAsync(topUp);
        reloaded.Status.Should().Be(TopUpStatus.Confirmed);
    }

    [PostgresFact]
    public async Task ConcurrentGatewayConfirmation_SettlesOnce_AndCreditsSingleAmount()
    {
        var topUp = await CreateGatewayTopUpAsync(amountNoor: 100m);

        var results = await Task.WhenAll(
            ConfirmGatewayAsync(topUp, "bank-transaction-2"),
            ConfirmGatewayAsync(topUp, "bank-transaction-2"));

        results.Should().Contain(x => x.Result.IsSuccess);

        var (regular, gift) = await BalancesAsync();
        regular.Should().Be(100m);
        gift.Should().Be(0m);

        var entries = await TopUpEntriesAsync(topUp);
        entries.Should().HaveCount(2);
        entries.Sum(x => x.Amount).Should().Be(0m);
        entries.Select(x => x.TransactionId).Distinct().Should().ContainSingle();
    }

    [PostgresFact]
    public async Task LateConfirmation_AfterReviewOpened_CreditsNoorOnce_WithoutPurchase()
    {
        var topUp = await CreateGatewayTopUpAsync(amountNoor: 100m);
        await OpenReviewAsync(topUp, "review-case-late");

        var confirmed = await ConfirmGatewayAsync(topUp, "bank-transaction-3");

        confirmed.Result.IsSuccess.Should().BeTrue();

        var (regular, gift) = await BalancesAsync();
        regular.Should().Be(100m);
        gift.Should().Be(0m);

        var entries = await TopUpEntriesAsync(topUp);
        entries.Should().HaveCount(2);
        entries.Single(x => x.Amount > 0).Amount.Should().Be(100m);

        // تأیید دیرهنگام فقط نور اضافه می‌کند: هیچ کسر و خریدی در دفتر مالی ثبت نمی‌شود.
        var userEntries = await UserEntriesAsync();
        userEntries.Should().OnlyContain(x => x.Amount > 0);
        userEntries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Regular);
        (await PurchaseCountAsync()).Should().Be(0);

        var reloaded = await ReloadTopUpAsync(topUp);
        reloaded.Status.Should().Be(TopUpStatus.Confirmed);
    }

    [PostgresFact]
    public async Task LateConfirmation_WithManualRefundRecorded_IsRejected_AndCreditsNothing()
    {
        var topUp = await CreateGatewayTopUpAsync(amountNoor: 100m);
        await OpenReviewAsync(topUp, "review-case-refund");
        await RecordManualRefundAsync(topUp, "review-case-refund", "manual-refund-ref-1");

        var confirmed = await ConfirmGatewayAsync(topUp, "bank-transaction-4");

        confirmed.Result.IsSuccess.Should().BeFalse();

        var (regular, gift) = await BalancesAsync();
        regular.Should().Be(0m);
        gift.Should().Be(0m);
        (await TopUpEntriesAsync(topUp)).Should().BeEmpty();
    }

    [PostgresFact]
    public async Task TopUpAndGiftCredits_KeepSeparateBucketsAndTheirOwnReferenceTypes()
    {
        var topUp = await CreateGatewayTopUpAsync(amountNoor: 100m);
        await ConfirmGatewayAsync(topUp, "bank-transaction-5");

        var giftBatchId = Guid.NewGuid().ToString();
        _ = await CreditGiftAsync(40m, giftBatchId);

        var (regular, gift) = await BalancesAsync();
        regular.Should().Be(100m);
        gift.Should().Be(40m);

        var topUpEntries = await TopUpEntriesAsync(topUp);
        topUpEntries.Should().HaveCount(2);
        topUpEntries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Regular);
        topUpEntries.Should().OnlyContain(x => x.ReferenceType == LedgerReferenceType.TopUp);

        var giftEntries = await EntriesByReferenceAsync(giftBatchId);
        giftEntries.Should().HaveCount(2);
        giftEntries.Sum(x => x.Amount).Should().Be(0m);
        giftEntries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Gift);
        giftEntries.Should().OnlyContain(x => x.ReferenceType == LedgerReferenceType.AdminGrant);

        // ماندهٔ هر سبد باید با جمع سطرهای همان سبد در دفتر مالی برابر باشد.
        var userEntries = await UserEntriesAsync();
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Regular).Sum(x => x.Amount).Should().Be(regular);
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Gift).Sum(x => x.Amount).Should().Be(gift);
    }

    private async Task<TopUpRequest> CreateGatewayTopUpAsync(decimal amountNoor)
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var topUp = TopUpRequest.CreateGateway(_customer.Id, amountNoor, RialPerNoor);
        db.Add(topUp);
        await db.SaveChangesAsync();
        return topUp;
    }

    private async Task<GatewayConfirmationOutcome> ConfirmGatewayAsync(TopUpRequest topUp, string externalTransactionId)
    {
        using var scope = _provider!.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IGatewayTopUpService>();
        var amountRial = topUp.AmountRial!.Value;

        return await gateway.ConfirmAsync(new ConfirmGatewayTopUpCommand(
            topUp.ClientReferenceCode,
            GatewayName,
            externalTransactionId,
            "bank-reference-1",
            amountRial,
            amountRial));
    }

    private async Task OpenReviewAsync(TopUpRequest topUp, string caseId)
    {
        using var scope = _provider!.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IGatewayTopUpService>();

        var opened = await gateway.OpenReviewAsync(topUp.ClientReferenceCode, caseId, FinancialReviewReasonCodes.NoCallback);

        opened.IsSuccess.Should().BeTrue();
    }

    private async Task RecordManualRefundAsync(TopUpRequest topUp, string caseId, string manualRefundReference)
    {
        using var scope = _provider!.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IGatewayTopUpService>();
        var evidence = new FinancialReviewEvidence(
            Guid.NewGuid(),
            ExpectedRevision: 1,
            CheckedReportDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MatchingDepositFound: null,
            Note: "بازپرداخت دستی آزمون",
            DepositReference: null,
            DepositDate: null,
            DepositAmountRial: null,
            ManualRefundReference: manualRefundReference);

        var resolved = await gateway.ResolveManualReviewAsync(topUp.ClientReferenceCode, caseId,
            FinancialReviewOutcomeCodes.ManualRefund, null, evidence, Guid.NewGuid(), "admin-test");

        resolved.IsSuccess.Should().BeTrue();
    }

    private async Task<NoorCreditResult> CreditGiftAsync(decimal amountNoor, string batchReferenceId)
    {
        using var scope = _provider!.CreateScope();
        var noorCredit = scope.ServiceProvider.GetRequiredService<INoorCreditService>();

        var result = await noorCredit.CreditAsync(new NoorCreditRequest(
            UserId: _customer.Id,
            AmountNoor: amountNoor,
            BalanceType: LedgerBalanceType.Gift,
            ReferenceType: LedgerReferenceType.AdminGrant,
            ReferenceId: batchReferenceId));

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return result;
    }

    private async Task<(decimal Regular, decimal Gift)> BalancesAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<WalletEntity>().AsNoTracking().SingleAsync(x => x.UserId == _customer.Id);
        return (wallet.WithdrawableBalance, wallet.GiftBalance);
    }

    private async Task<TopUpRequest> ReloadTopUpAsync(TopUpRequest topUp)
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<TopUpRequest>().AsNoTracking()
            .SingleAsync(x => x.Id == new TopUpRequestId(topUp.Id.Value));
    }

    private async Task<List<LedgerEntry>> TopUpEntriesAsync(TopUpRequest topUp)
        => await EntriesByReferenceAsync(topUp.Id.ToString());

    private async Task<List<LedgerEntry>> EntriesByReferenceAsync(string referenceId)
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<LedgerEntry>().AsNoTracking()
            .Where(x => x.ReferenceId == referenceId)
            .ToListAsync();
    }

    private async Task<List<LedgerEntry>> UserEntriesAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<WalletEntity>().AsNoTracking().SingleAsync(x => x.UserId == _customer.Id);
        return await db.Set<LedgerEntry>().AsNoTracking()
            .Where(x => x.AccountId == wallet.AccountId)
            .ToListAsync();
    }

    private async Task<int> PurchaseCountAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<CoursePurchase>().AsNoTracking().CountAsync();
    }
}
