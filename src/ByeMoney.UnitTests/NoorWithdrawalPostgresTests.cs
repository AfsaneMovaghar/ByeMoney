using ByeMoney.Application;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.Queries.GetRefundableTopUps;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using System.Globalization;

namespace ByeMoney.UnitTests;

/// <summary>
/// پذیرش PostgreSQL خروج نور (D4): کسر فقط از نور عادی، تکرارناپذیری، سریال‌شدن با خرید،
/// یک نتیجه برای ثبت واریز و «پرداخت نشد» هم‌زمان، و جبران بدون برگشت مضاعف (T25).
/// </summary>
public sealed class NoorWithdrawalPostgresTests : IAsyncLifetime
{
    private const string MigrationBeforeWithdrawals = "20261107164552_RenameCartItemExternalItemId";
    private const decimal CurrentRialPerNoor = 10_000m;
    private const decimal TopUpRialPerNoor = 8_000m;

    private readonly string _schema = "withdrawal_test_" + Guid.NewGuid().ToString("N");
    private readonly User _buyer = User.CreateInitial("withdraw-buyer-" + Guid.NewGuid().ToString("N"));
    private readonly User _admin = User.CreateInitial("withdraw-admin-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();
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
        services.AddSingleton(_pricing.Object);
        _provider = services.BuildServiceProvider();

        _pricing
            .Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Add(_buyer);
        db.Add(_admin);
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        await provisioning.GetOrCreateUserWalletAsync(_buyer.Id, default);
        await provisioning.GetOrCreateSystemAccountAsync(default);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>()
            .SetValueAsync("ConversionRate:RialToNoor", CurrentRialPerNoor.ToString(CultureInfo.InvariantCulture));
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
    public async Task Create_DebitsOnlyRegularNoor_WithBalancedLedger()
    {
        await SeedWalletAsync(regular: 100m, gift: 50m);

        var result = await CreateAsync(Request(amountNoor: 60m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(NoorWithdrawalStatus.PendingPayment));
        result.Value.Kind.Should().Be(nameof(NoorWithdrawalKind.AdminDiscretionary));
        result.Value.UserExternalId.Should().Be(_buyer.ExternalUserId);
        result.Value.CreatedByExternalUserId.Should().Be(_admin.ExternalUserId);

        var wallet = await GetWalletAsync();
        wallet.WithdrawableBalance.Should().Be(40m);
        wallet.GiftBalance.Should().Be(50m);

        var entries = await LedgerAsync(LedgerReferenceType.Withdrawal);
        entries.Should().HaveCount(2);
        entries.Sum(x => x.Amount).Should().Be(0m);
        entries.Should().OnlyContain(x =>
            x.BalanceType == LedgerBalanceType.Regular &&
            x.ReferenceId == result.Value.Id.ToString() &&
            x.PerformedByUserId == _admin.Id);
        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task Create_WithOnlyGiftNoor_IsRejectedWithoutLedger()
    {
        await SeedWalletAsync(regular: 10m, gift: 500m);

        var result = await CreateAsync(Request(amountNoor: 20m));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.InsufficientBalance);
        (await LedgerAsync(LedgerReferenceType.Withdrawal)).Should().BeEmpty();
        (await GetWalletAsync()).GiftBalance.Should().Be(500m);
    }

    [PostgresFact]
    public async Task Create_WithPaymentEvidence_IsRecordedAsPaidInOneStep()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);

        var result = await CreateAsync(Request(amountNoor: 30m) with
        {
            PaymentReference = "BANK-1",
            ReceiptId = "receipt-a.png"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(NoorWithdrawalStatus.Paid));
        result.Value.PaymentReference.Should().Be("BANK-1");
        result.Value.HasReceipt.Should().BeTrue();
        result.Value.PaidByExternalUserId.Should().Be(_admin.ExternalUserId);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(70m);
    }

    [PostgresFact]
    public async Task ConcurrentRepeatsOfOneOperation_DebitOnce_AndChangedPayloadConflicts()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        var request = Request(amountNoor: 25m);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(request)));

        results.Should().OnlyContain(x => x.IsSuccess);
        results.Select(x => x.Value.Id).Distinct().Should().ContainSingle();
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(75m);
        (await LedgerAsync(LedgerReferenceType.Withdrawal)).Should().HaveCount(2);

        var changed = await CreateAsync(request with { AmountRial = 26m * CurrentRialPerNoor });
        changed.Status.Should().Be(ResultStatus.Conflict);
        changed.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.IdempotencyConflict);
    }

    [PostgresFact]
    public async Task WithdrawalAndPurchaseOnLastRegularNoor_OnlyOneSucceeds()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["c-1"], null);
        cart.SetQuote([new("c-1", "دوره ۱", 100m, 0m, 0m, 100m)]);
        using (var scope = _provider.CreateScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartStore>();
            (await carts.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();
        }

        var withdrawal = CreateAsync(Request(amountNoor: 100m));
        var purchase = PayAsync(cart);
        await Task.WhenAll(withdrawal, purchase);

        new[] { withdrawal.Result.IsSuccess, purchase.Result.IsSuccess }.Count(x => x).Should().Be(1);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(0m);
        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task ConcurrentMarkPaidAndReverse_HaveExactlyOneOutcome()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        var created = await CreateAsync(Request(amountNoor: 40m));

        var paid = WithService(s => s.MarkPaidAsync(_admin.Id.Value, created.Value.Id, "BANK-2", "receipt-b.png", default));
        var reversed = WithService(s => s.ReverseAsync(_admin.Id.Value, created.Value.Id, "کارت مسدود بود", default));
        await Task.WhenAll(paid, reversed);

        new[] { paid.Result.IsSuccess, reversed.Result.IsSuccess }.Count(x => x).Should().Be(1);
        var reversals = await LedgerAsync(LedgerReferenceType.WithdrawalReversal);
        var wallet = await GetWalletAsync();
        if (reversed.Result.IsSuccess)
        {
            reversals.Should().HaveCount(2);
            wallet.WithdrawableBalance.Should().Be(100m);
        }
        else
        {
            reversals.Should().BeEmpty();
            wallet.WithdrawableBalance.Should().Be(60m);
        }

        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task RepeatedReverse_CreditsOnce_AndPaidCannotBeReversed()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        var first = await CreateAsync(Request(amountNoor: 40m));

        var reverseResults = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            WithService(s => s.ReverseAsync(_admin.Id.Value, first.Value.Id, "واریز برگشت خورد", default))));

        reverseResults.Should().OnlyContain(x => x.IsSuccess);
        reverseResults[0].Value.Status.Should().Be(nameof(NoorWithdrawalStatus.Reversed));
        reverseResults[0].Value.ReversedByExternalUserId.Should().Be(_admin.ExternalUserId);
        (await LedgerAsync(LedgerReferenceType.WithdrawalReversal)).Should().HaveCount(2);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(100m);

        var second = await CreateAsync(Request(amountNoor: 10m));
        var paid = await WithService(s => s.MarkPaidAsync(_admin.Id.Value, second.Value.Id, "BANK-3", "receipt-c.png", default));
        paid.IsSuccess.Should().BeTrue();
        var repeatPaid = await WithService(s => s.MarkPaidAsync(_admin.Id.Value, second.Value.Id, "BANK-3", "receipt-c.png", default));
        repeatPaid.IsSuccess.Should().BeTrue();
        var otherPaid = await WithService(s => s.MarkPaidAsync(_admin.Id.Value, second.Value.Id, "BANK-4", "receipt-c.png", default));
        otherPaid.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.NotPending);

        var reverseAfterPaid = await WithService(s => s.ReverseAsync(_admin.Id.Value, second.Value.Id, "دیر", default));
        reverseAfterPaid.Status.Should().Be(ResultStatus.Conflict);
        reverseAfterPaid.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.NotPending);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(90m);
        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task Create_ConvertsRialToNoorWithCurrentRate()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);

        var result = await CreateAsync(Request(amountNoor: 1m) with { AmountRial = 25_000m });

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountRial.Should().Be(25_000m);
        result.Value.AmountNoor.Should().Be(2.5m);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(97.5m);
        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task Retry_AfterRateChange_ReturnsSameWithdrawal_WithoutSecondDebit()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        var request = Request(amountNoor: 20m);

        var first = await CreateAsync(request);
        await SetRateAsync(CurrentRialPerNoor * 2);
        var retry = await CreateAsync(request);
        await SetRateAsync(CurrentRialPerNoor);

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Id.Should().Be(first.Value.Id);
        retry.Value.AmountNoor.Should().Be(20m);
        (await GetWalletAsync()).WithdrawableBalance.Should().Be(80m);
        (await LedgerAsync(LedgerReferenceType.Withdrawal)).Should().HaveCount(2);
    }

    [PostgresFact]
    public async Task Create_WhenRialRoundsToZeroNoor_IsRejected()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        await SetRateAsync(100_000m);

        var result = await CreateAsync(Request(amountNoor: 1m) with { AmountRial = 1m });
        await SetRateAsync(CurrentRialPerNoor);

        result.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.AmountTooSmall);
        (await LedgerAsync(LedgerReferenceType.Withdrawal)).Should().BeEmpty();
    }

    [PostgresFact]
    public async Task RefundableTopUps_ListsOnlyConfirmedTopUpsOfUser_AndMarksActiveRefund()
    {
        await SeedWalletAsync(regular: 200m, gift: 0m);
        var confirmed = await SeedConfirmedTopUpAsync(_buyer.Id, amountNoor: 50m);
        await SeedConfirmedTopUpAsync(_buyer.Id, amountNoor: 10m, rialPerNoor: null);
        await SeedPendingTopUpAsync(_buyer.Id, amountNoor: 20m);

        var before = await ListRefundableTopUpsAsync();
        before.Should().ContainSingle();
        before[0].Id.Should().Be(confirmed);
        before[0].AmountRial.Should().Be(50m * TopUpRialPerNoor);
        before[0].RialPerNoor.Should().Be(TopUpRialPerNoor);
        before[0].HasActiveRefund.Should().BeFalse();

        var refund = await CreateAsync(RefundRequest(confirmed, 50m * TopUpRialPerNoor));
        refund.IsSuccess.Should().BeTrue();
        (await ListRefundableTopUpsAsync())[0].HasActiveRefund.Should().BeTrue();

        await WithService(s => s.ReverseAsync(_admin.Id.Value, refund.Value.Id, "برگشت", default));
        (await ListRefundableTopUpsAsync())[0].HasActiveRefund.Should().BeFalse();
    }

    [PostgresFact]
    public async Task DuplicatePaymentRefund_UsesTopUpRate_AndIsLimitedToOneActiveRefundPerConfirmedTopUp()
    {
        await SeedWalletAsync(regular: 200m, gift: 0m);
        var topUpId = await SeedConfirmedTopUpAsync(_buyer.Id, amountNoor: 50m);

        var tooMuch = await CreateAsync(RefundRequest(topUpId, 50m * TopUpRialPerNoor + 1m));
        tooMuch.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.AmountExceedsTopUp);

        var partial = await CreateAsync(RefundRequest(topUpId, 80_000m));
        partial.IsSuccess.Should().BeTrue();
        partial.Value.AmountNoor.Should().Be(10m);
        (await WithService(s => s.ReverseAsync(_admin.Id.Value, partial.Value.Id, "مبلغ اشتباه بود", default)))
            .IsSuccess.Should().BeTrue();

        var first = await CreateAsync(RefundRequest(topUpId, 50m * TopUpRialPerNoor));
        first.IsSuccess.Should().BeTrue();
        first.Value.RelatedTopUpId.Should().Be(topUpId);
        first.Value.AmountNoor.Should().Be(50m);

        var second = await CreateAsync(RefundRequest(topUpId, 50m * TopUpRialPerNoor));
        second.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.TopUpAlreadyRefunded);

        (await WithService(s => s.ReverseAsync(_admin.Id.Value, first.Value.Id, "شبا اشتباه بود", default)))
            .IsSuccess.Should().BeTrue();
        var retry = await CreateAsync(RefundRequest(topUpId, 50m * TopUpRialPerNoor));
        retry.IsSuccess.Should().BeTrue();

        var otherUser = User.CreateInitial("withdraw-other-" + Guid.NewGuid().ToString("N"));
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(otherUser);
            await db.SaveChangesAsync();
        }

        var foreignTopUp = await SeedConfirmedTopUpAsync(otherUser.Id, amountNoor: 50m);
        var foreign = await CreateAsync(RefundRequest(foreignTopUp, 10m * TopUpRialPerNoor));
        foreign.ErrorCode.Should().Be(NoorWithdrawalErrorCodes.TopUpInvalid);
        await AssertWalletMatchesLedgerAsync();
    }

    [PostgresFact]
    public async Task Migration_DownAndUp_PassWithWithdrawalData()
    {
        await SeedWalletAsync(regular: 100m, gift: 0m);
        await CreateAsync(Request(amountNoor: 30m));
        var reversed = await CreateAsync(Request(amountNoor: 20m));
        await WithService(s => s.ReverseAsync(_admin.Id.Value, reversed.Value.Id, "برگشت", default));

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GetService<IMigrator>().MigrateAsync(MigrationBeforeWithdrawals);
        }

        await using (var connection = new NpgsqlConnection(SchemaConnection()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT w.\"Balance\" - COALESCE((SELECT SUM(l.\"Amount\") FROM \"LedgerEntries\" l " +
                "WHERE l.\"AccountId\" = w.\"AccountId\" AND l.\"BalanceType\" = 'Regular'), 0) " +
                "FROM \"Wallets\" w WHERE w.\"UserId\" = @userId", connection);
            command.Parameters.AddWithValue("userId", _buyer.Id.Value);
            ((decimal)(await command.ExecuteScalarAsync())!).Should().Be(0m);
        }

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
        }

        (await GetWalletAsync()).WithdrawableBalance.Should().Be(100m);
        var again = await CreateAsync(Request(amountNoor: 10m));
        again.IsSuccess.Should().BeTrue();
    }

    private CreateNoorWithdrawalCommand Request(decimal amountNoor) => new(
        Guid.NewGuid(),
        _buyer.ExternalUserId,
        nameof(NoorWithdrawalKind.AdminDiscretionary),
        null,
        amountNoor * CurrentRialPerNoor,
        "علی رضایی",
        "6037991234567890",
        null,
        "خروج به صلاحدید ادمین",
        null,
        null);

    private CreateNoorWithdrawalCommand RefundRequest(Guid topUpId, decimal amountRial) =>
        Request(amountNoor: 1m) with
        {
            AmountRial = amountRial,
            Kind = nameof(NoorWithdrawalKind.DuplicatePaymentRefund),
            RelatedTopUpId = topUpId,
            BeneficiaryCardNumber = null,
            BeneficiaryIban = "IR123456789012345678901234",
            Description = "پرداخت دوباره در درگاه"
        };

    private Task<Result<NoorWithdrawalResponse>> CreateAsync(CreateNoorWithdrawalCommand request) =>
        WithService(s => s.CreateAsync(_admin.Id.Value, request, default));

    private async Task<Result<NoorWithdrawalResponse>> WithService(
        Func<NoorWithdrawalService, Task<Result<NoorWithdrawalResponse>>> action)
    {
        using var scope = _provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NoorWithdrawalService>());
    }

    private async Task<Result<Cart>> PayAsync(Cart cart)
    {
        using var scope = _provider.CreateScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartStore>();
        return await carts.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default);
    }

    private async Task SeedWalletAsync(decimal regular, decimal gift)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        var systemAccount = await db.Set<Account>().SingleAsync(x => x.Type == AccountType.System);

        if (regular > 0)
        {
            wallet.Credit(regular, LedgerBalanceType.Regular);
            AddLedgerPair(db, wallet.AccountId, systemAccount.Id, regular, LedgerReferenceType.TopUp, LedgerBalanceType.Regular);
        }

        if (gift > 0)
        {
            wallet.Credit(gift, LedgerBalanceType.Gift);
            AddLedgerPair(db, wallet.AccountId, systemAccount.Id, gift, LedgerReferenceType.AdminGrant, LedgerBalanceType.Gift);
        }

        await db.SaveChangesAsync();
    }

    private static void AddLedgerPair(
        ApplicationDbContext db,
        AccountId userAccount,
        AccountId systemAccount,
        decimal amount,
        LedgerReferenceType referenceType,
        LedgerBalanceType balanceType)
    {
        var transactionId = Guid.NewGuid();
        db.Add(LedgerEntry.Create(userAccount, amount, transactionId, referenceType, balanceType: balanceType));
        db.Add(LedgerEntry.Create(systemAccount, -amount, transactionId, referenceType, balanceType: balanceType));
    }

    private async Task<Guid> SeedConfirmedTopUpAsync(
        UserId userId, decimal amountNoor, decimal? rialPerNoor = TopUpRialPerNoor)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var topUp = TopUpRequest.Create(userId, amountNoor, PaymentMethod.CardToCard, rialPerNoor: rialPerNoor);
        topUp.Confirm("ext-" + Guid.NewGuid().ToString("N")).IsSuccess.Should().BeTrue();
        db.Add(topUp);
        await db.SaveChangesAsync();
        return topUp.Id.Value;
    }

    private async Task SeedPendingTopUpAsync(UserId userId, decimal amountNoor)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(TopUpRequest.Create(userId, amountNoor, PaymentMethod.CardToCard, rialPerNoor: TopUpRialPerNoor));
        await db.SaveChangesAsync();
    }

    private async Task SetRateAsync(decimal rialPerNoor)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>()
            .SetValueAsync("ConversionRate:RialToNoor", rialPerNoor.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<IReadOnlyList<RefundableTopUpResponse>> ListRefundableTopUpsAsync()
    {
        using var scope = _provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        return await sender.Send(new GetRefundableTopUpsQuery(_buyer.ExternalUserId));
    }

    private async Task<Wallet> GetWalletAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<Wallet>().AsNoTracking().SingleAsync(x => x.UserId == _buyer.Id);
    }

    private async Task<List<LedgerEntry>> LedgerAsync(LedgerReferenceType referenceType)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<LedgerEntry>().AsNoTracking().Where(x => x.ReferenceType == referenceType).ToListAsync();
    }

    private async Task AssertWalletMatchesLedgerAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().AsNoTracking().SingleAsync(x => x.UserId == _buyer.Id);
        var entries = await db.Set<LedgerEntry>().AsNoTracking().ToListAsync();

        entries.Sum(x => x.Amount).Should().Be(0m);
        var userEntries = entries.Where(x => x.AccountId == wallet.AccountId).ToList();
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Regular).Sum(x => x.Amount)
            .Should().Be(wallet.WithdrawableBalance);
        userEntries.Where(x => x.BalanceType == LedgerBalanceType.Gift).Sum(x => x.Amount)
            .Should().Be(wallet.GiftBalance);
    }

    private string SchemaConnection() =>
        new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema, Pooling = false }.ConnectionString;
}
