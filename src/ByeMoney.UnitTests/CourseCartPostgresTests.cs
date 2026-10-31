using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
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

public sealed class CourseCartPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "cart_test_" + Guid.NewGuid().ToString("N");
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;
    private readonly User _buyer = User.CreateInitial("cart-test-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();
    private ICartStore Store => _provider.GetRequiredService<ICartStore>();
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
        // Ù†Ø§Ù… schema ÙÙ‚Ø· Ø§Ø² GUID Ø§ÛŒÙ† Ø¢Ø²Ù…ÙˆÙ† Ø³Ø§Ø®ØªÙ‡ Ø´Ø¯Ù‡ Ø§Ø³Øª.
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<Cart> Ready(string[]? ids = null, bool free = false)
    {
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ids ?? ["a", "b"], "OFF");
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();
        return await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value, cart.RequestedItemSignatures.Select((id, index) =>
            new CartQuoteItem(id, "Ø¯ÙˆØ±Ù‡", 105, 0, free ? 105 : index == 0 ? 11 : 10, free ? 0 : index == 0 ? 94 : 95)).ToArray(), cart.ReservationId, default);
    }
    private async Task<(decimal Balance, int Purchases, decimal Debit, decimal Net)> Financials()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.UserId == _buyer.Id);
        var ledger = await db.Set<LedgerEntry>().ToListAsync();
        return (wallet.WithdrawableBalance, await db.Set<CoursePurchase>().CountAsync(), -ledger.Where(x => x.Amount < 0 && x.ReferenceType == LedgerReferenceType.Purchase).Sum(x => x.Amount), ledger.Sum(x => x.Amount));
    }

    [CartPostgresFact]
    public async Task ChapterPaymentPersistsIdentityAndDebitsQuotedAllocationOnce()
    {
        var chapterId = Guid.NewGuid().ToString();
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, [chapterId], null);
        await Store.CreateAsync(cart, default);
        cart = await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value,
            [new(chapterId, "ÙØµÙ„", 120, 0, 20, 100, ProductItemType.CourseChapter, "parent")],
            cart.ReservationId, default);

        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purchase = await db.Set<CoursePurchase>().AsNoTracking().SingleAsync();
        purchase.Snapshot.ItemType.Should().Be(ProductItemType.CourseChapter);
        purchase.Snapshot.ExternalProductId.Should().Be(chapterId);
        purchase.Snapshot.ExternalParentId.Should().Be("parent");
        purchase.Snapshot.PriceInNoorAtPurchaseTime.Should().Be(100);
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Items.Single().ItemType
            .Should().Be(ProductItemType.CourseChapter);
        (await Financials()).Should().Be((900m, 1, 100m, 0m));
    }

    [CartPostgresFact]
    public async Task ConcurrentCartsForSameChapterHaveOnlyOneFinancialEffect()
    {
        var chapterId = Guid.NewGuid().ToString();
        async Task<Cart> ChapterCart()
        {
            var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, [chapterId], null);
            await Store.CreateAsync(cart, default);
            return await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value,
                [new(chapterId, "ÙØµÙ„", 100, 0, 0, 100, ProductItemType.CourseChapter, "parent")],
                cart.ReservationId, default);
        }

        var first = await ChapterCart();
        var second = await ChapterCart();
        var results = await Task.WhenAll(
            Store.PayAsync(first.Id, _buyer.Id.Value, first.ReservationId, default),
            Store.PayAsync(second.Id, _buyer.Id.Value, second.ReservationId, default));

        results.Count(result => result.IsSuccess).Should().Be(1);
        (await Financials()).Should().Be((900m, 1, 100m, 0m));

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var duplicate = CoursePurchase.Create(_buyer.Id,
            ProductSnapshot.Create(chapterId, "ÙØµÙ„", 100,
                itemType: ProductItemType.CourseChapter, externalParentId: "parent"));
        db.Add(duplicate);
        Func<Task> saveDuplicate = async () => await db.SaveChangesAsync();
        var failure = await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
        failure.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("IX_CoursePurchases_UserId_ExternalProductId");
    }

    [CartPostgresFact]
    public async Task OwningWholeCoursePreventsChapterDebit()
    {
        var parent = await Ready(["parent"]);
        (await Store.PayAsync(parent.Id, _buyer.Id.Value, parent.ReservationId, default)).IsSuccess.Should().BeTrue();
        var before = await Financials();
        var chapterId = Guid.NewGuid().ToString();
        var chapter = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, [chapterId], null);
        await Store.CreateAsync(chapter, default);
        chapter = await Store.SetQuoteAsync(chapter.Id, _buyer.Id.Value,
            [new(chapterId, "ÙØµÙ„", 100, 0, 0, 100, ProductItemType.CourseChapter, "parent")],
            chapter.ReservationId, default);

        (await Store.PayAsync(chapter.Id, _buyer.Id.Value, chapter.ReservationId, default)).IsSuccess.Should().BeFalse();
        (await Financials()).Should().Be(before);
    }

    [CartPostgresFact]
    public async Task StrapiRejectionOfWholeChapteredCoursePreventsPayment()
    {
        var cart = await Ready(["chaptered-parent"]);
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CartService>();

        (await service.PurchaseAsync(_buyer.Id.Value, cart.Id, default)).IsSuccess.Should().BeFalse();
        (await Financials()).Should().Be((1000m, 0, 0m, 0m));
    }

    [CartPostgresFact]
    public async Task PurchaseReferenceRateIsFrozenAtPaymentAcrossReplayAndFreePurchase()
    {
        var paid = await Ready();
        using var scope = _provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();
        var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
        var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
        // رویهٔ مصوب ۲۰۲۶-۱۰-۳۱: دادهٔ سبد پیش از عبور از مهاجرت ادغام پاک می‌شود؛ خرید، شارژ و Ledger حفظ‌اند.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CourseCarts\"");
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
        var items = System.Text.Json.JsonSerializer.Deserialize<CartQuoteItem[]>(json);
        items!.Select(x => new CartItem(x.ExternalCourseId, x.Title, x.BasePriceInNoor, x.PriceInNoor)).Should().Equal(cart.Items);
        await migrator.MigrateAsync();
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default)).Should().BeNull();
        (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"CourseCartItems\"").SingleAsync()).Should().Be(0);
        (await Financials()).Should().Be((811m, 2, 189m, 0m));
    }

    [CartPostgresFact]
    public async Task MigrationChainWithWipedCartsPreservesFinancialsAndMergedAllocation()
    {
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["a", "b"], "OFF");
        await Store.CreateAsync(cart, default);
        cart = await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value,
            [new("a", "الف", 200, 10, 20, 170), new("b", "ب", 100, 100, 0, 0)], cart.ReservationId, default);
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();

        // بازگشت تا پیش از مهاجرت ادغام: مسیر Down باید مبلغ تخصیصی را به جدول تخصیص برگرداند.
        await migrator.MigrateAsync("20261018095758_MoveCourseCartDiscountsToCart");
        var restoredAllocations = await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM \"CourseCartPurchaseAllocations\"").SingleAsync();
        var restoredAllocationSum = await db.Database.SqlQuery<decimal>(
            $"SELECT COALESCE(SUM(\"AmountInNoor\"), 0) AS \"Value\" FROM \"CourseCartPurchaseAllocations\"").SingleAsync();
        restoredAllocations.Should().Be(2);
        restoredAllocationSum.Should().Be(170m);

        // رویهٔ مصوب ۲۰۲۶-۱۰-۳۱: دادهٔ سبد پیش از عبور از مهاجرت ادغام پاک می‌شود؛ خرید و Ledger حفظ‌اند.
        await migrator.MigrateAsync("20261018074939_NormalizeCourseCartItems");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CourseCarts\"");
        await migrator.MigrateAsync();
        (await Financials()).Should().Be((1000m, 0, 0m, 0m));

        var fresh = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ["a", "b"], "OFF");
        (await Store.CreateAsync(fresh, default)).IsSuccess.Should().BeTrue();
        fresh = await Store.SetQuoteAsync(fresh.Id, _buyer.Id.Value,
            [new("a", "الف", 200, 10, 20, 170), new("b", "ب", 100, 100, 0, 0)], fresh.ReservationId, default);
        fresh.Items.Select(x => x.AllocatedAmountInNoor).Should().Equal(170m, 0m);
        fresh.ItemDiscountInNoor.Should().Be(110);
        fresh.CouponDiscountInNoor.Should().Be(20);
        fresh.Total.Should().Be(170);
        (await Store.PayAsync(fresh.Id, _buyer.Id.Value, fresh.ReservationId, default)).IsSuccess.Should().BeTrue();
        (await Financials()).Should().Be((830m, 2, 170m, 0m));
    }
    [CartPostgresFact]
    public async Task RelationalItemsPreserveSnapshotOrderAcrossReplayAndRecovery()
    {
        var cart = await Ready(["z", "A", "a"]);
        var loaded = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        loaded.Items.Should().Equal(cart.Items);
        loaded.Items.Select(x => x.ExternalCourseId).Should().Equal("A", "a", "z");
        (await Store.CreateAsync(Cart.Create(cart.Id, _buyer.Id.Value, _buyer.ExternalUserId,
            cart.RequestedItemSignatures, "OFF"), default)).Value.Items.Should().Equal(cart.Items);
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
        CartQuoteItem[] quote = [new("a", "Ù‚ÛŒÙ…Øª ØªØ§Ø²Ù‡", 200, 10, 20, 170), new("b", "Ø±Ø§ÛŒÚ¯Ø§Ù†", 100, 100, 0, 0)];
        await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value, quote, reopened.ReservationId, default);
        var loaded = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        loaded.Items.Should().Equal(quote.Select(x => new CartItem(x.ExternalCourseId, x.Title, x.BasePriceInNoor, x.PriceInNoor)));
        loaded.ItemDiscountInNoor.Should().Be(110);
        loaded.CouponDiscountInNoor.Should().Be(20);
        loaded.Items.Sum(x => x.AllocatedAmountInNoor).Should().Be(170);
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
        var result = await scope.ServiceProvider.GetRequiredService<CartService>().CancelAsync(_buyer.Id.Value, cart.Id, default);
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
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.Status.Should().Be(CartStatus.Ready);
    }
    [CartPostgresFact]
    public async Task CancelCannotReleaseAwaitingBankResultOrPaidCart()
    {
        var cart = await AwaitingTopUp();
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
        if (current.Status == CartStatus.Paid)
            (await Financials()).Should().Be((811m, 2, 189m, 0m));
        else
        {
            current.Status.Should().Be(CartStatus.Failed);
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
        (await Store.CreateAsync(Cart.Create(cart.Id, Guid.NewGuid(), "other", ["a", "b"], "OFF"), default)).IsFailure.Should().BeTrue();
    }
    [CartPostgresFact]
    public async Task StrapiOutageAfterDebitIsRecoveredWithoutSecondDebit()
    {
        var cart = await Ready();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        using (var scope = _provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
            var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
    private async Task<Cart> AwaitingTopUp()
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
    private async Task ConfirmTopUp(Cart cart)
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
        var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
        var service = scope.ServiceProvider.GetRequiredService<CartService>();
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
            topUp!.CancelGateway("SEP", "Ù„ØºÙˆ Ù‚Ø·Ø¹ÛŒ Ø¢Ø²Ù…ÙˆÙ†");
            await db.SaveChangesAsync();
        }
        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));
        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        var previews = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            using var requestScope = _provider.CreateScope();
            return await requestScope.ServiceProvider.GetRequiredService<CartService>()
                .PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        }));
        previews.Should().OnlyContain(x => x.IsSuccess && x.Value.CartId == cart.Id && x.Value.PriceInNoor == 270);
        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        (await Store.FindByTopUpAsync(previousTopUp, _buyer.Id.Value, default))!.Id.Should().Be(cart.Id);
        // Ù¾Ø§Ø³Ø® Ùˆ Ú©Ø§Ø± Ø¨Ø§Ø²ÛŒØ§Ø¨ÛŒ Ù†Ø³Ù„ Ù‚Ø¨Ù„ÛŒ Ù†Ø¨Ø§ÛŒØ¯ Ø³Ø¨Ø¯ ØªØ§Ø²Ù‡ Ø±Ø§ Ø¨Ø¨Ù†Ø¯Ù†Ø¯ ÛŒØ§ Ø±Ø²Ø±Ùˆ ØªØ§Ø²Ù‡ Ø±Ø§ Ø¢Ø²Ø§Ø¯Ø´Ø¯Ù‡ Ø§Ø¹Ù„Ø§Ù… Ú©Ù†Ù†Ø¯.
        await Store.FailAsync(cart.Id, _buyer.Id.Value, false, cart.ReservationId, default);
        await Store.RecordSynchronizationAsync(cart.Id, _buyer.Id.Value, true, null, cart.ReservationId, default);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsFailure.Should().BeTrue();
        current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.Status.Should().Be(CartStatus.Ready); current.Synchronized.Should().BeFalse();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        attempts.Should().OnlyContain(x => x.IsSuccess);
        attempts.Select(x => x.Value.TopUpRequestId).Distinct().Should().ContainSingle();
        attempts[0].Value.TopUpRequestId.Should().NotBe(previousTopUp);
        attempts[0].Value.AmountRial.Should().Be(250 * 137);
        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Set<Cart>().CountAsync()).Should().Be(1);
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
    public async Task PendingPaymentAllowsRepaymentWithFreshQuoteAndRetainsOldTopUp()
    {
        var cart = await AwaitingTopUp();
        var previousTopUp = cart.TopUpRequestId!.Value;

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        var preview = await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        preview.IsSuccess.Should().BeTrue();
        preview.Value.CartId.Should().Be(cart.Id);
        preview.Value.PriceInNoor.Should().Be(270);

        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        current.TopUpRequestId.Should().BeNull();
        current.TopUpRequestIds.Should().Contain(previousTopUp);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        attempts.Should().OnlyContain(x => x.IsSuccess);
        var retryTopUpId = attempts[0].Value.TopUpRequestId;
        retryTopUpId.Should().NotBe(previousTopUp);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Set<TopUpRequest>().CountAsync()).Should().Be(2);
        var oldTopUp = await verifyDb.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
        oldTopUp!.Status.Should().Be(TopUpStatus.Pending);
    }

    [CartPostgresFact]
    public async Task AmbiguousGatewayResultMovesTopUpToUnderReviewAndAllowsCartRepayment()
    {
        var cart = await AwaitingTopUp();
        var previousTopUp = cart.TopUpRequestId!.Value;

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
            var result = topUp!.RecordGatewayResult(
                "evt-ambig-1",
                "SEP",
                GatewayResultKinds.Unknown,
                "ambig-ref",
                "ambig-rrn",
                "-2",
                10000,
                10000,
                DateTime.UtcNow);
            result.IsSuccess.Should().BeTrue();
            topUp.Status.Should().Be(TopUpStatus.UnderReview);
            topUp.IsUnderReview.Should().BeTrue();

            topUp.OpenFinancialReview("case-ambig-1", FinancialReviewReasonCodes.VerifyUnknown);
            topUp.ReviewCaseId.Should().Be("case-ambig-1");
            topUp.Status.Should().Be(TopUpStatus.UnderReview);

            await db.SaveChangesAsync();
        }

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        var preview = await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        preview.IsSuccess.Should().BeTrue();
        preview.Value.PriceInNoor.Should().Be(270);

        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        current.TopUpRequestId.Should().BeNull();
        current.TopUpRequestIds.Should().Contain(previousTopUp);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        attempts.Should().OnlyContain(x => x.IsSuccess);
        var retryTopUpId = attempts[0].Value.TopUpRequestId;
        retryTopUpId.Should().NotBe(previousTopUp);

        var order = await service.GetAsync(_buyer.Id.Value, cart.Id, default);
        order.Value.Status.Should().Be("AwaitingTopUp");
        order.Value.TopUpRequestId.Should().Be(retryTopUpId);
        order.Value.IsUnderReview.Should().BeFalse();

        var cartInDb = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        cartInDb.TopUpRequestIds.Should().Contain(previousTopUp);
    }

    [CartPostgresFact]
    public async Task LateConfirmationOfOldTopUpUnderReviewOnlyCreditsNoorWithoutDuplicatePurchase()
    {
        var cart = await AwaitingTopUp();
        var previousTopUpId = cart.TopUpRequestId!.Value;

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUpId));
            topUp!.RecordGatewayResult(
                "evt-ambig-2",
                "SEP",
                GatewayResultKinds.Unknown,
                "ambig-ref-2",
                "ambig-rrn-2",
                "-2",
                10000,
                10000,
                DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);

        var retryAttempt = await Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default);
        var retryTopUpId = retryAttempt.Value.TopUpRequestId;

        using (var settleScope = _provider.CreateScope())
        {
            var db = settleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var retryTopUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(retryTopUpId));
            retryTopUp!.ConfirmGateway("retry-bank-ref", "retry-rrn", "SEP").IsSuccess.Should().BeTrue();
            await settleScope.ServiceProvider.GetRequiredService<ITopUpSettlementService>().SettleAsync(retryTopUp);
        }

        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        await service.ProcessAsync(current, default);
        (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("Paid");

        var financialsAfterPurchase = await Financials();
        financialsAfterPurchase.Purchases.Should().Be(2);

        // ØªØ£ÛŒÛŒØ¯ Ø¯ÛŒØ±Ù‡Ù†Ú¯Ø§Ù… Ø´Ø§Ø±Ú˜ Ù‚Ø¨Ù„ÛŒ Ø¯Ø± Ø­Ø§Ù„ Ø¨Ø±Ø±Ø³ÛŒ
        using (var lateScope = _provider.CreateScope())
        {
            var db = lateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var oldTopUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUpId));
            oldTopUp!.ConfirmGateway("late-bank-ref", "late-rrn", "SEP").IsSuccess.Should().BeTrue();
            await lateScope.ServiceProvider.GetRequiredService<ITopUpSettlementService>().SettleAsync(oldTopUp);
        }

        // Ù¾Ø±Ø¯Ø§Ø²Ø´ Ù…Ø¬Ø¯Ø¯ Ø³Ø¨Ø¯ ØªÙˆØ³Ø· Ø±ÛŒÚ©Ø§ÙˆØ±ÛŒ Ù¾Ø³â€ŒØ²Ù…ÛŒÙ†Ù‡
        await service.ProcessAsync(current, default);

        var financialsAfterLateConfirm = await Financials();
        financialsAfterLateConfirm.Purchases.Should().Be(2);
        financialsAfterLateConfirm.Debit.Should().Be(financialsAfterPurchase.Debit);
        financialsAfterLateConfirm.Balance.Should().Be(financialsAfterPurchase.Balance + 169m);
    }

    [CartPostgresFact]
    public async Task ManualRefundProtectionRejectsSubsequentGatewayConfirmation()
    {
        var cart = await AwaitingTopUp();
        var topUpId = cart.TopUpRequestId!.Value;

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(topUpId));
            topUp!.RecordGatewayResult("evt-rev-fail", "SEP", GatewayResultKinds.ReverseFailed, "ref-1", "rrn-1", null, 10000, 10000, DateTime.UtcNow);
            topUp.Status.Should().Be(TopUpStatus.UnderReview);

            topUp.OpenFinancialReview("case-refund-prot", FinancialReviewReasonCodes.ReverseUnknown);
            topUp.ResolveManualReview("case-refund-prot", FinancialReviewOutcomeCodes.ManualRefund, null,
                new(Guid.NewGuid(), 1, new DateOnly(2026, 10, 27), null, null, null, null, null, "bank-refund-slip-999"),
                Guid.NewGuid(), "Ù…Ø¯ÛŒØ± Ù…Ø§Ù„ÛŒ").IsSuccess.Should().BeTrue();

            topUp.Status.Should().Be(TopUpStatus.ManuallyRefunded);
            topUp.ManualRefundReference.Should().Be("bank-refund-slip-999");
            await db.SaveChangesAsync();
        }

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(topUpId));
            var recordLate = topUp!.RecordGatewayResult("evt-late-after-refund", "SEP", GatewayResultKinds.Verified, "late-ref", "late-rrn", "0", 10000, 10000, DateTime.UtcNow);
            recordLate.IsFailure.Should().BeTrue();

            var confirmResult = topUp.ConfirmGateway("late-ref", "late-rrn", "SEP");
            confirmResult.IsFailure.Should().BeTrue();

            topUp.Status.Should().Be(TopUpStatus.ManuallyRefunded);
        }
    }

    [CartPostgresFact]
    public async Task ConcurrentCreateTopUpAttemptsProduceSingleActiveTopUpAndPreserveHistory()
    {
        var cart = await AwaitingTopUp();
        var firstTopUpId = cart.TopUpRequestId!.Value;

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(firstTopUpId));
            topUp!.RecordGatewayResult("evt-unknown", "SEP", GatewayResultKinds.Unknown, "ref-u", "rrn-u", "-2", 10000, 10000, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);

        var tasks = Enumerable.Range(0, 5).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)).ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.IsSuccess);
        var activeTopUpIds = results.Select(r => r.Value.TopUpRequestId).Distinct().ToList();
        activeTopUpIds.Should().ContainSingle();
        var newTopUpId = activeTopUpIds[0];
        newTopUpId.Should().NotBe(firstTopUpId);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Set<TopUpRequest>().CountAsync()).Should().Be(2);

        var finalCart = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        finalCart.TopUpRequestId.Should().Be(newTopUpId);
        finalCart.TopUpRequestIds.Should().Contain(firstTopUpId);
    }

    [CartPostgresFact]
    public async Task BuyerCourseUniqueConstraintExistsInEfModelAndPostgresDatabase()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Û±. Ø¨Ø±Ø±Ø³ÛŒ Ù…Ø¯Ù„ EF Core
        var entityType = db.Model.FindEntityType(typeof(CoursePurchase));
        entityType.Should().NotBeNull();
        var ownership = entityType!.FindNavigation(nameof(CoursePurchase.Snapshot))!.TargetEntityType;
        var efIndex = ownership.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_CoursePurchases_UserId_ExternalProductId");
        efIndex.Should().NotBeNull();
        efIndex!.IsUnique.Should().BeTrue();
        efIndex.Properties.Select(p => p.GetColumnName()).Should().Contain(["UserId", "ExternalProductId"]);

        // Û². Ø¨Ø±Ø±Ø³ÛŒ ÙˆØ¬ÙˆØ¯ ÙˆØ§Ù‚Ø¹ÛŒ Ø¯Ø± Ú©Ø§ØªØ§Ù„ÙˆÚ¯ Ø¯ÛŒØªØ§Ø¨ÛŒØ³ Ù¾Ø³ØªÚ¯Ø±Ø³ (pg_indexes)
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'CoursePurchases' AND indexname = 'IX_CoursePurchases_UserId_ExternalProductId';";
        await using var reader = await cmd.ExecuteReaderAsync();
        var foundInPg = await reader.ReadAsync();
        foundInPg.Should().BeTrue();
        var indexDef = reader.GetString(1);
        indexDef.Should().Contain("UNIQUE INDEX");
        indexDef.Should().Contain("\"UserId\", \"ExternalProductId\"");
        await reader.CloseAsync();

        // Û³. Ø¨Ø±Ø±Ø³ÛŒ Ø±ÙØªØ§Ø± Ø¹ÛŒÙ†ÛŒ Ø¯ÛŒØªØ§Ø¨ÛŒØ³ Ø¯Ø± Ø«Ø¨Øª ØªÚ©Ø±Ø§Ø±ÛŒ (Unique Violation 23505)
        var courseId = Guid.NewGuid().ToString("N");
        var first = CoursePurchase.Create(_buyer.Id, ProductSnapshot.Create(courseId, "Ø¯ÙˆØ±Ù‡ Ø¢Ø²Ù…Ø§ÛŒØ´ÛŒ", 100));
        db.Add(first);
        await db.SaveChangesAsync();

        var duplicate = CoursePurchase.Create(_buyer.Id, ProductSnapshot.Create(courseId, "Ø¯ÙˆØ±Ù‡ Ø¢Ø²Ù…Ø§ÛŒØ´ÛŒ Ù…Ø¬Ø¯Ø¯", 100));
        db.Add(duplicate);
        Func<Task> saveDuplicate = async () => await db.SaveChangesAsync();
        var ex = await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
        var pgEx = ex.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        pgEx.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        pgEx.ConstraintName.Should().Be("IX_CoursePurchases_UserId_ExternalProductId");
    }

    [CartPostgresFact]
    public async Task UnresolvedPaymentRetriesSameCartWithFreshQuoteAndRetainsOldTopUp()
    {
        var cart = await AwaitingTopUp();
        var previousTopUp = cart.TopUpRequestId!.Value;
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
            topUp!.OpenFinancialReview("case-unres", FinancialReviewReasonCodes.NoCallback);
            topUp.ResolveManualReview("case-unres", FinancialReviewOutcomeCodes.NoMatchingDeposit, null,
                new(Guid.NewGuid(), 1, new DateOnly(2026, 9, 30), false, null, null, null, null, null),
                Guid.NewGuid(), "Ø­Ø³Ø§Ø¨Ø¯Ø§Ø±").IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        // Ù…Ø³ÛŒØ± Û±: Ú©Ø§Ø±Ø¨Ø± Ø¯Ø± Ø­Ø§Ù„ÛŒ Ú©Ù‡ Ø³Ø¨Ø¯ Ù‡Ù†ÙˆØ² AwaitingTopUp Ø§Ø³Øª Ù…Ø³ØªÙ‚ÛŒÙ…Ø§Ù‹ PrepareAsync Ø±Ø§ ØµØ¯Ø§ Ù…ÛŒâ€ŒØ²Ù†Ø¯
        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        var preview = await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        preview.IsSuccess.Should().BeTrue();
        preview.Value.CartId.Should().Be(cart.Id);
        preview.Value.PriceInNoor.Should().Be(270);

        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        current.TopUpRequestId.Should().BeNull();
        current.TopUpRequestIds.Should().Contain(previousTopUp);

        // Ù¾Ø±Ø¯Ø§Ø®Øª ØªØ§Ø²Ù‡ Ø¨Ø§ Ø´Ù†Ø§Ø³Ù‡ Ùˆ Ø¯Ø±Ú¯Ø§Ù‡ ØªØ§Ø²Ù‡
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default)));
        attempts.Should().OnlyContain(x => x.IsSuccess);
        var retryTopUpId = attempts[0].Value.TopUpRequestId;
        retryTopUpId.Should().NotBe(previousTopUp);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var retryTopUp = await verifyDb.Set<TopUpRequest>().FindAsync(new TopUpRequestId(retryTopUpId));
        retryTopUp!.ConfirmGateway("retry-bank-ref", "retry-rrn", "SEP").IsSuccess.Should().BeTrue();
        await verifyScope.ServiceProvider.GetRequiredService<ITopUpSettlementService>().SettleAsync(retryTopUp);

        current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        await service.ProcessAsync(current, default);
        (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value.Status.Should().Be("Paid");
        (await Financials()).Should().Be((.2m, 2, 270m, 0m));

        // Ù¾Ø±Ø¯Ø§Ø®Øª Ø¯ÛŒØ±Ù‡Ù†Ú¯Ø§Ù… Ø´Ø§Ø±Ú˜ Ù‚Ø¯ÛŒÙ…ÛŒ Ø¨Ù‡ ØµÙˆØ±Øª Verified Ù†Ø¨Ø§ÛŒØ¯ Ø«Ø¨Øª Ø´ÙˆØ¯
        var oldTopUp = await verifyDb.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
        oldTopUp!.RecordGatewayResult("evt-late", "SEP", GatewayResultKinds.Verified, "late-ref", "late-rrn", "0", 10000, 10000, DateTime.UtcNow)
            .IsFailure.Should().BeTrue();
    }

    [CartPostgresFact]
    public async Task ManuallyRefundedPaymentRetriesSameCartAndProtectsRefundRecord()
    {
        var cart = await AwaitingTopUp();
        var previousTopUp = cart.TopUpRequestId!.Value;
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var topUp = await db.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
            topUp!.OpenFinancialReview("case-refund", FinancialReviewReasonCodes.NoCallback);
            topUp.ResolveManualReview("case-refund", FinancialReviewOutcomeCodes.ManualRefund, null,
                new(Guid.NewGuid(), 1, new DateOnly(2026, 9, 30), null, null, null, null, null, "refund-ref-123"),
                Guid.NewGuid(), "Ø­Ø³Ø§Ø¨Ø¯Ø§Ø±").IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        // Ù…Ø³ÛŒØ± Û²: Ø¨Ú©â€ŒÚ¯Ø±Ø§Ù†Ø¯ ProcessAsync Ø§Ø¨ØªØ¯Ø§ Ø§Ø¬Ø±Ø§ Ù…ÛŒâ€ŒØ´ÙˆØ¯ Ùˆ Ø³Ø¨Ø¯ Ø±Ø§ Ø¨Ù‡ Failed Ù…Ù†ØªÙ‚Ù„ Ù…ÛŒâ€ŒÚ©Ù†Ø¯
        using var recoveryScope = _provider.CreateScope();
        await recoveryScope.ServiceProvider.GetRequiredService<CartService>().ProcessAsync(cart, default);

        var failedCart = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        failedCart.Status.Should().Be(CartStatus.Failed);

        _pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(
                new(request.CartId, request.BuyerExternalUserId, request.CouponCode,
                    [new("a", "Ø§Ù„Ù", 200, 0, 20, 180), new("b", "Ø¨", 100, 0, 10, 90)])));

        using var serviceScope = _provider.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<CartService>();
        var preview = await service.PrepareAsync(_buyer.Id.Value, cart.Id, ["a", "b"], "OFF", default);
        preview.IsSuccess.Should().BeTrue();
        preview.Value.PriceInNoor.Should().Be(270);

        var current = (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!;
        current.ReservationId.Should().NotBe(cart.ReservationId);
        current.TopUpRequestId.Should().BeNull();
        current.TopUpRequestIds.Should().Contain(previousTopUp);

        var createRetry = await Store.CreateTopUpAsync(cart.Id, _buyer.Id.Value, default);
        createRetry.IsSuccess.Should().BeTrue();
        createRetry.Value.TopUpRequestId.Should().NotBe(previousTopUp);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var oldTopUp = await verifyDb.Set<TopUpRequest>().FindAsync(new TopUpRequestId(previousTopUp));
        oldTopUp!.ManualRefundReference.Should().Be("refund-ref-123");
        oldTopUp.Status.Should().Be(TopUpStatus.ManuallyRefunded);

        // Ù¾Ø±Ø¯Ø§Ø®Øª Ø¯ÛŒØ±Ù‡Ù†Ú¯Ø§Ù… Ø¨Ø±Ø§ÛŒ Ø´Ø§Ø±Ú˜ Ø¨Ø§ Ù…Ø±Ø¬Ø¹ Ø¨Ø§Ø²Ù¾Ø±Ø¯Ø§Ø®Øª Ø¯Ø³ØªÛŒ Ù†Ø§Ù…Ø¹ØªØ¨Ø± Ø§Ø³Øª Ùˆ Ù†Ø¨Ø§ÛŒØ¯ Ù¾Ø°ÛŒØ±ÙØªÙ‡ Ø´ÙˆØ¯
        oldTopUp.RecordGatewayResult("evt-late-refund", "SEP", GatewayResultKinds.Verified, "late-ref", "late-rrn", "0", 10000, 10000, DateTime.UtcNow)
            .IsFailure.Should().BeTrue();
    }
}
