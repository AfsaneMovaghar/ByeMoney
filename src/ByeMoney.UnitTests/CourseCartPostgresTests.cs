using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Interfaces;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
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

namespace ByeMoney.UnitTests;

public sealed class CartPostgresFactAttribute : FactAttribute
{
    public CartPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1")
            Skip = "اجرای آزمون مستقل PostgreSQL با BYEMONEY_CART_PG_TESTS=1 فعال می‌شود.";
    }
}

public sealed class CourseCartPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "cart_test_" + Guid.NewGuid().ToString("N");
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;
    private readonly User _buyer = User.CreateInitial("cart-test-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();
    private ICourseCartStore Store => _provider.GetRequiredService<ICourseCartStore>();
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1") return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ByeMoney.sln"))) directory = directory.Parent;
        var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(directory!.FullName, "src", "ByeMoney.API"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true).AddEnvironmentVariables().Build();
        _adminConnection = configuration.GetConnectionString("DefaultConnection")!;
        await using (var connection = new NpgsqlConnection(_adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema };
        var services = new ServiceCollection().AddLogging().AddApplicationServices().AddInfrastructureServices(configuration);
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(builder.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));
        services.AddSingleton(_pricing.Object);
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Add(_buyer); await db.SaveChangesAsync();
        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        var wallet = await provisioning.GetOrCreateUserWalletAsync(_buyer.Id); wallet.Wallet.ApplyCredit(1000);
        await provisioning.GetOrCreateSystemAccountAsync(); await db.SaveChangesAsync();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }
    public async Task DisposeAsync()
    {
        if (_provider is null) return;
        await _provider.DisposeAsync();
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        // نام schema فقط از GUID این آزمون ساخته شده است.
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<CourseCart> Ready(string[]? ids = null, bool free = false)
    {
        var cart = CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ids ?? ["a", "b"], "OFF");
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();
        return await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value, cart.ExternalCourseIds.Select((id, index) =>
            new CourseCartQuoteItem(id, "دوره", 105, 0, free ? 105 : index == 0 ? 11 : 10, free ? 0 : index == 0 ? 94 : 95)).ToArray(), cart.ReservationId, default);
    }
    private async Task<(decimal Balance, int Purchases, decimal Debit, decimal Net)> Financials()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        var ledger = await db.Set<LedgerEntry>().ToListAsync();
        return (wallet.Balance, await db.Set<CoursePurchase>().CountAsync(), -ledger.Where(x => x.Amount < 0 && x.ReferenceType == LedgerReferenceType.Purchase).Sum(x => x.Amount), ledger.Sum(x => x.Amount));
    }

    [CartPostgresFact]
    public async Task PurchaseReferenceRateIsFrozenAtPaymentAcrossReplayAndFreePurchase()
    {
        var paid = await Ready();
        using var scope = _provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        await settings.SetValueAsync("ConversionRate:RialToNoor", "137");
        (await service.PurchaseAsync(_buyer.Id.Value, paid.Id, default)).IsSuccess.Should().BeTrue();
        await settings.SetValueAsync("ConversionRate:RialToNoor", "991");
        (await service.PurchaseAsync(_buyer.Id.Value, paid.Id, default)).IsSuccess.Should().BeTrue();

        var free = await Ready(["free"], free: true);
        (await service.PurchaseAsync(_buyer.Id.Value, free.Id, default)).IsSuccess.Should().BeTrue();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purchases = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
        purchases.Where(x => x.CartId == paid.Id).Should().HaveCount(2)
            .And.OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 137m);
        purchases.Single(x => x.CartId == free.Id).Snapshot.ReferenceRialPerNoorAtPurchaseTime.Should().Be(991m);
        (await Financials()).Should().Be((811m, 3, 189m, 0m));
    }

    [CartPostgresFact]
    public async Task MissingOrInvalidReferenceRateDoesNotPreventCartPayment()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Set<Domain.Modules.Settings.SystemSetting>().ExecuteDeleteAsync();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        var first = await Ready(["missing"]);
        (await service.PurchaseAsync(_buyer.Id.Value, first.Id, default)).IsSuccess.Should().BeTrue();
        db.Add(Domain.Modules.Settings.SystemSetting.Create("ConversionRate:RialToNoor", "invalid"));
        await db.SaveChangesAsync();
        var second = await Ready(["invalid"]);
        (await service.PurchaseAsync(_buyer.Id.Value, second.Id, default)).IsSuccess.Should().BeTrue();

        var purchases = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
        purchases.Should().HaveCount(2).And.OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == null);
        (await Financials()).Should().Be((812m, 2, 188m, 0m));
    }
    [CartPostgresFact]
    public async Task ReferenceRateMigrationLeavesExistingPurchasesNull()
    {
        var cart = await Ready();
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018095758_MoveCourseCartDiscountsToCart");
        await scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>()
            .SetValueAsync("ConversionRate:RialToNoor", "991");
        await migrator.MigrateAsync();

        var purchases = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
        purchases.Should().HaveCount(2).And.OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == null);
        (await Financials()).Should().Be((811m, 2, 189m, 0m));
    }

    [CartPostgresFact]
    public async Task MigrationClearsTestCartsButPreservesPurchasesWalletAndLedger()
    {
        var cart = await Ready();
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018061504_AddCourseCartPaymentRetries");
        var json = await db.Database.SqlQuery<string>($"SELECT \"Items\"::text AS \"Value\" FROM \"CourseCarts\" WHERE \"Id\" = {cart.Id}").SingleAsync();
        var items = System.Text.Json.JsonSerializer.Deserialize<CourseCartQuoteItem[]>(json);
        items!.Select(x => new CourseCartItem(x.ExternalCourseId, x.Title, x.BasePriceInNoor)).Should().Equal(cart.Items);
        await migrator.MigrateAsync();
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default)).Should().BeNull();
        (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"CourseCartItems\"").SingleAsync()).Should().Be(0);
        (await Financials()).Should().Be((811m, 2, 189m, 0m));
    }

    [CartPostgresFact]
    public async Task DiscountMigrationPreservesExistingCartAndFinancialAllocation()
    {
        var cart = CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["a", "b"], "OFF");
        await Store.CreateAsync(cart, default);
        cart = await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value,
            [new("a", "الف", 200, 10, 20, 170), new("b", "ب", 100, 100, 0, 0)], cart.ReservationId, default);
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018074939_NormalizeCourseCartItems");
        await migrator.MigrateAsync();

        var loaded = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        loaded.Items.Should().Equal(cart.Items);
        loaded.PurchaseAllocations.Should().Equal(cart.PurchaseAllocations);
        loaded.ItemDiscountInNoor.Should().Be(110);
        loaded.CouponDiscountInNoor.Should().Be(20);
        loaded.Total.Should().Be(170);
        loaded.Status.Should().Be(CourseCartStatus.Ready);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Financials()).Should().Be((830m, 2, 170m, 0m));
    }

    [CartPostgresFact]
    public async Task RelationalItemsPreserveSnapshotOrderAcrossReplayAndRecovery()
    {
        var cart = await Ready(["z", "A", "a"]);
        var loaded = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        loaded.Items.Should().Equal(cart.Items);
        loaded.Items.Select(x => x.ExternalCourseId).Should().Equal("A", "a", "z");
        (await Store.CreateAsync(CourseCart.Create(cart.Id, _buyer.Id.Value, _buyer.ExternalUserId,
            cart.ExternalCourseIds, "OFF"), default)).Value.Items.Should().Equal(cart.Items);
        await Store.FailAsync(cart.Id, _buyer.Id.Value, false, cart.ReservationId, default);
        (await Store.GetPendingAsync(default)).Single(x => x.Id == cart.Id).Items.Should().Equal(cart.Items);
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"CourseCartItems\" WHERE \"CartId\" = {cart.Id}").SingleAsync()).Should().Be(3);
    }

    [CartPostgresFact]
    public async Task ReopeningRemovesOldRowsAndReplacesQuoteWithoutDuplicates()
    {
        var cart = await Ready();
        await Store.FailAsync(cart.Id, _buyer.Id.Value, false, cart.ReservationId, default);
        await Store.RecordSynchronizationAsync(cart.Id, _buyer.Id.Value, true, null, cart.ReservationId, default);
        var reopened = (await Store.ReopenAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).Value;
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Items.Should().BeEmpty();
        CourseCartQuoteItem[] quote = [new("a", "قیمت تازه", 200, 10, 20, 170), new("b", "رایگان", 100, 100, 0, 0)];
        await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value, quote, reopened.ReservationId, default);
        var loaded = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        loaded.Items.Should().Equal(quote.Select(x => new CourseCartItem(x.ExternalCourseId, x.Title, x.BasePriceInNoor)));
        loaded.ItemDiscountInNoor.Should().Be(110);
        loaded.CouponDiscountInNoor.Should().Be(20);
        loaded.PurchaseAllocations.Sum(x => x.AmountInNoor).Should().Be(170);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, reopened.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Financials()).Should().Be((830m, 2, 170m, 0m));
    }

    [CartPostgresFact]
    public async Task DatabaseRejectsInvalidAmountsDuplicateCoursesAndMissingCart()
    {
        var cart = await Ready();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Func<Task> negative = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CourseCartItems\" SET \"PriceInNoor\" = -1 WHERE \"CartId\" = {cart.Id}");
        Func<Task> inconsistent = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CourseCarts\" SET \"ItemDiscountInNoor\" = -1 WHERE \"Id\" = {cart.Id}");
        Func<Task> fractional = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CourseCartItems\" SET \"PriceInNoor\" = 94.5 WHERE \"CartId\" = {cart.Id}");
        Func<Task> duplicate = () => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CourseCartItems\" SELECT * FROM \"CourseCartItems\" WHERE \"CartId\" = {cart.Id}");
        Func<Task> orphan = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CourseCartItems\" SET \"CartId\" = {Guid.NewGuid()} WHERE \"CartId\" = {cart.Id}");
        foreach (var invalid in new[] { negative, inconsistent, fractional })
            (await invalid.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await orphan.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Items.Should().Equal(cart.Items);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CourseCarts\" WHERE \"Id\" = {cart.Id}");
        (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"CourseCartItems\" WHERE \"CartId\" = {cart.Id}").SingleAsync()).Should().Be(0);
        (await Financials()).Should().Be((1000m, 0, 0m, 0m));
    }
    [CartPostgresFact]
    public async Task CancelReadyCartReleasesReservationAndCannotDebit()
    {
        var cart = await Ready();
        using var scope = _provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CourseCartService>().CancelAsync(_buyer.Id.Value, cart.Id, default);
        result.Value.Status.Should().Be("Failed");
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Synchronized.Should().BeTrue();
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsFailure.Should().BeTrue();
        (await Financials()).Should().Be((1000m, 0, 0m, 0m));
        _pricing.Verify(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()), Times.Once);
    }
    [CartPostgresFact]
    public async Task CancelCannotChangeAnotherBuyersCart()
    {
        var cart = await Ready();
        (await Store.CancelAsync(cart.Id, Guid.NewGuid(), default)).ErrorCode.Should().Be(CartErrorCodes.NotFound);
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Status.Should().Be(CourseCartStatus.Ready);
    }
    [CartPostgresFact]
    public async Task CancelCannotReleaseAwaitingBankResultOrPaidCart()
    {
        var cart = await AwaitingTopUp();
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        (await service.CancelAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("AwaitingTopUp");
        await ConfirmTopUp(cart);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await service.CancelAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("Paid");
        _pricing.Verify(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()), Times.Never);
        (await Financials()).Should().Be((.2m, 2, 189m, 0m));
    }
    [CartPostgresFact]
    public async Task CancellationRacingPaymentCannotReleasePaidCartOrDebitFailedCart()
    {
        var cart = await Ready();
        await Task.WhenAll(Store.CancelAsync(cart.Id, _buyer.Id.Value, default), Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default));
        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        if (current.Status == CourseCartStatus.Paid)
            (await Financials()).Should().Be((811m, 2, 189m, 0m));
        else
        {
            current.Status.Should().Be(CourseCartStatus.Failed);
            (await Financials()).Should().Be((1000m, 0, 0m, 0m));
        }
    }
    [CartPostgresFact]
    public async Task ParallelReplayDebitsWholeCartOnceAndPreservesLedgerSum()
    {
        var cart = await Ready();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)));
        results.Should().OnlyContain(x => x.IsSuccess);
        var values = await Financials();
        values.Should().Be((811m, 2, 189m, 0m));
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<CoursePurchase>().ToListAsync()).Should().OnlyContain(x => x.CartId == cart.Id && x.Status == CoursePurchaseStatus.Debited);
    }
    [CartPostgresFact]
    public async Task OverlappingConcurrentCartsCannotPurchaseSameCourseTwice()
    {
        var first = await Ready(["a", "b"]); var second = await Ready(["a", "c"]);
        var results = await Task.WhenAll(Store.PayAsync(first.Id, _buyer.Id.Value, first.ReservationId, default), Store.PayAsync(second.Id, _buyer.Id.Value, second.ReservationId, default));
        results.Count(x => x.IsSuccess).Should().Be(1);
        (await Financials()).Should().Be((811m, 2, 189m, 0m));
    }
    [CartPostgresFact]
    public async Task FreeCartHasPurchasesAndNoLedgerOrDebit()
    {
        var cart = await Ready(free: true);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Financials()).Should().Be((1000m, 2, 0m, 0m));
    }
    [CartPostgresFact]
    public async Task AnotherBuyerCannotReadCartOrReuseItsId()
    {
        var cart = await Ready();
        (await Store.FindAsync(cart.Id, Guid.NewGuid(), default)).Should().BeNull();
        (await Store.CreateAsync(CourseCart.Create(cart.Id, Guid.NewGuid(), "other", ["a", "b"], "OFF"), default)).IsFailure.Should().BeTrue();
    }
    [CartPostgresFact]
    public async Task StrapiOutageAfterDebitIsRecoveredWithoutSecondDebit()
    {
        var cart = await Ready();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        using (var scope = _provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
            var response = await service.PurchaseAsync(_buyer.Id.Value, cart.Id, default);
            response.Value.Status.Should().Be("Paid");
            response.Value.Courses.Should().OnlyContain(x => x.PurchaseStatus == "DeliveryPending");
            var pending = await Store.FindAsync(cart.Id, _buyer.Id.Value, default);
            pending!.SyncAttempts.Should().Be(1);
            pending.LastSyncErrorCode.Should().Be(CartErrorCodes.Unavailable);
        }
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        using (var scope = _provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
            await service.ProcessAsync((await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!, default);
            (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value.Courses.Should().OnlyContain(x => x.PurchaseStatus == "Completed");
        }
        (await Financials()).Should().Be((811m, 2, 189m, 0m));
    }
    [CartPostgresFact]
    public async Task InsufficientBalanceDoesNotPartiallyPurchaseOrDebit()
    {
        var cart = await Ready();
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id)).ApplyDebit(900);
            await db.SaveChangesAsync();
        }
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).ErrorCode.Should().Be(CartErrorCodes.InsufficientBalance);
        (await Financials()).Should().Be((100m, 0, 0m, 0m));
    }
    private async Task<CourseCart> AwaitingTopUp()
    {
        var cart = await Ready();
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id)).ApplyDebit(979.8m);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>().SetValueAsync("ConversionRate:RialToNoor", "137");
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        results.Should().OnlyContain(x => x.IsSuccess);
        results.Select(x => x.Value.TopUpRequestId).Distinct().Should().ContainSingle();
        results[0].Value.AmountRial.Should().Be(169 * 137);
        return (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
    }
    private async Task ConfirmTopUp(CourseCart cart)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(cart.TopUpRequestId!.Value));
        topUp!.AmountNoor.Should().Be(169);
        topUp.PendingItems.Should().BeEmpty();
        topUp.ConfirmGateway("cart-test-bank", "reference", "SEP").IsSuccess.Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<ITopUpSettlementService>().SettleAsync(topUp);
    }
    [CartPostgresFact]
    public async Task ConfirmedTopUpIsPurchasedByRecoveryWithoutBrowserAndPreservesFrozenPrice()
    {
        var cart = await AwaitingTopUp();
        using (var rateScope = _provider.CreateScope())
        {
            await rateScope.ServiceProvider.GetRequiredService<ISystemSettingRepository>()
                .SetValueAsync("ConversionRate:RialToNoor", "991");
        }
        await ConfirmTopUp(cart);
        (await Store.GetPendingAsync(default)).Should().Contain(x => x.Id == cart.Id);
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        await service.ProcessAsync(cart, default); await service.ProcessAsync(cart, default);
        var result = await service.GetAsync(_buyer.Id.Value, cart.Id, default);
        result.Value.Status.Should().Be("Paid"); result.Value.Courses.Should().OnlyContain(x => x.PurchaseStatus == "Completed");
        result.Value.PriceInNoor.Should().Be(189); result.Value.TopUpStatus.Should().Be("Confirmed");
        (await Financials()).Should().Be((.2m, 2, 189m, 0m));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purchases = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
        purchases.Should().OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 991m);
        var topUp = await db.Set<TopUpRequest>().AsNoTracking().SingleAsync();
        topUp.RialPerNoorSnapshot.Should().Be(137m);
        _pricing.Verify(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        (await Store.FindByTopUpAsync(cart.TopUpRequestId!.Value, _buyer.Id.Value, default))!.Id.Should().Be(cart.Id);
        (await Store.FindByTopUpAsync(cart.TopUpRequestId.Value, Guid.NewGuid(), default)).Should().BeNull();
    }
    [CartPostgresFact]
    public async Task FailedPurchaseAfterConfirmedTopUpKeepsCreditedNoorAndReleasesCoupon()
    {
        var cart = await AwaitingTopUp(); await ConfirmTopUp(cart);
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        await service.ProcessAsync(cart, default);
        (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("Failed");
        (await Financials()).Should().Be((189.2m, 0, 0m, 0m));
        _pricing.Verify(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()), Times.Once);
        _pricing.Verify(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>()), Times.Never);
    }

    [CartPostgresFact]
    public async Task RejectedPaymentRetriesSameCartWithFreshQuoteAndRetainsEveryAttempt()
    {
        var cart = await AwaitingTopUp();
        var previousTopUp = cart.TopUpRequestId!.Value;
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
            topUp!.CancelGateway("SEP", "لغو قطعی آزمون");
            await db.SaveChangesAsync();
        }
        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "الف", 200, 0, 20, 180), new("b", "ب", 100, 0, 10, 90)])));
        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CourseCartService>();
        var previews = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            using var requestScope = _provider.CreateScope();
            return await requestScope.ServiceProvider.GetRequiredService<CourseCartService>()
                .PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        }));
        previews.Should().OnlyContain(x => x.IsSuccess && x.Value.CartId == cart.Id && x.Value.PriceInNoor == 270);
        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        (await Store.FindByTopUpAsync(previousTopUp, _buyer.Id.Value, default))!.Id.Should().Be(cart.Id);
        // پاسخ و کار بازیابی نسل قبلی نباید سبد تازه را ببندند یا رزرو تازه را آزادشده اعلام کنند.
        await Store.FailAsync(cart.Id, _buyer.Id.Value, false, cart.ReservationId, default);
        await Store.RecordSynchronizationAsync(cart.Id, _buyer.Id.Value, true, null, cart.ReservationId, default);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsFailure.Should().BeTrue();
        current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.Status.Should().Be(CourseCartStatus.Ready); current.Synchronized.Should().BeFalse();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        attempts.Should().OnlyContain(x => x.IsSuccess);
        attempts.Select(x => x.Value.TopUpRequestId).Distinct().Should().ContainSingle();
        attempts[0].Value.TopUpRequestId.Should().NotBe(previousTopUp);
        attempts[0].Value.AmountRial.Should().Be(250 * 137);
        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Set<CourseCart>().CountAsync()).Should().Be(1);
        (await verifyDb.Set<TopUpRequest>().CountAsync()).Should().Be(2);
        (await Financials()).Should().Be((20.2m, 0, 0m, 0m));
        var retryTopUp = await verifyDb.Set<TopUpRequest>().FindAsync(new TopUpRequestId(attempts[0].Value.TopUpRequestId));
        retryTopUp!.ConfirmGateway("retry-bank", "retry-reference", "SEP").IsSuccess.Should().BeTrue();
        await verifyScope.ServiceProvider.GetRequiredService<ITopUpSettlementService>().SettleAsync(retryTopUp);
        current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        await service.ProcessAsync(current, default);
        await service.ProcessAsync(cart, default);
        (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("Paid");
        (await Financials()).Should().Be((.2m, 2, 270m, 0m));
        _pricing.Verify(x => x.TransitionAsync(It.Is<CartTransitionRequest>(r => r.CartId == current.ReservationId),
            "release", It.IsAny<CancellationToken>()), Times.Never);
    }

    [CartPostgresFact]
    public async Task PendingOrUnresolvedBankPaymentCannotBeReopened()
    {
        var cart = await AwaitingTopUp();
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CourseCartService>();
        var preview = await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        preview.Value.Status.Should().Be("AwaitingTopUp");
        _pricing.Verify(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        (await Store.ReopenAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsFailure.Should().BeTrue();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TopUpRequests\" SET \"Status\" = 'Unresolved' WHERE \"Id\" = {cart.TopUpRequestId!.Value}");
        // مانند هر درخواست مستقل، وضعیت تازه در context تازه خوانده می‌شود.
        using var recoveryScope = _provider.CreateScope();
        await recoveryScope.ServiceProvider.GetRequiredService<CourseCartService>().ProcessAsync(cart, default);
        (await Store.ReopenAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsFailure.Should().BeTrue();
        (await Financials()).Should().Be((20.2m, 0, 0m, 0m));
    }
}
