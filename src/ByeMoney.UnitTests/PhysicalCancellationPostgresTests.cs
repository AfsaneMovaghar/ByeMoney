using ByeMoney.API.Controllers;
using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;
using ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminPendingPhysicalRestocks;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminPhysicalCancellationPreview;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyPhysicalCancellationPreview;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace ByeMoney.UnitTests;

/// <summary>
/// آزمون‌های PostgreSQL لغو فیزیکی و برگشت نور (C8، D09) با DI واقعی، migrationهای واقعی و schema اختصاصی هر آزمون.
/// </summary>
public sealed class PhysicalCancellationPostgresTests : IAsyncLifetime
{
    private const string Reason = "انصراف خریدار پیش از ارسال";

    private readonly string _schema = "physical_cancel_test_" + Guid.NewGuid().ToString("N");
    private readonly User _buyer = User.CreateInitial("cancel-buyer-" + Guid.NewGuid().ToString("N"));
    private readonly User _otherUser = User.CreateInitial("cancel-other-" + Guid.NewGuid().ToString("N"));
    private readonly User _admin = User.CreateInitial("cancel-admin-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();
    private readonly Mock<IPhysicalStockClient> _stock = new();
    private readonly List<PhysicalRestockRequest> _restocks = [];
    private readonly TestCurrentUser _currentUser = new();
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;

    private ICartStore Store => _provider.GetRequiredService<ICartStore>();

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1")
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
        _adminConnection = configuration.GetConnectionString("DefaultConnection")!;

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
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(builder.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));
        services.AddSingleton(new PhysicalRestockSettings { MaxAttempts = RestockMaxAttempts });
        services.AddSingleton(_pricing.Object);
        services.AddSingleton(_stock.Object);
        services.AddSingleton<ICurrentUserService>(_currentUser);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.AddRange(_buyer, _otherUser, _admin);
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        var wallet = await provisioning.GetOrCreateUserWalletAsync(_buyer.Id);
        wallet.Wallet.ApplyCredit(1000m);
        wallet.Wallet.ApplyGiftCredit(40m);
        await provisioning.GetOrCreateSystemAccountAsync();
        await db.SaveChangesAsync();

        _pricing
            .Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        StockClientReturns(Result.Success());
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
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    [CartPostgresFact]
    public async Task PartialQuantityCancellations_GiveRemainderToLastUnit_AndKeepExactRegularAndGiftTotals()
    {
        // کتاب: ۳ عدد × ۴۰ با ۲۰ تخفیف = ۱۰۰ نور؛ ارسال ۳۰. هدیهٔ ۴۰ بین قلم (۳۰) و ارسال (۱۰) تقسیم می‌شود.
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 30m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "book");
        purchase.RegularAmountInNoor.Should().Be(70m);
        purchase.GiftAmountInNoor.Should().Be(30m);
        (await BalanceAsync()).Should().Be((910m, 0m));

        var first = await AdminCancelAsync(purchase.Id.Value, 1);
        var second = await AdminCancelAsync(purchase.Id.Value, 1);

        first.Items.Single().Should().Match<PhysicalCancellationItemResponse>(x =>
            x.RegularRefundInNoor == 23m && x.GiftRefundInNoor == 10m);
        second.Items.Single().Should().Match<PhysicalCancellationItemResponse>(x =>
            x.RegularRefundInNoor == 23m && x.GiftRefundInNoor == 10m);
        first.TotalRefundInNoor.Should().Be(33m);
        second.TotalRefundInNoor.Should().Be(33m);
        first.ShippingRegularRefundInNoor.Should().Be(0m);
        second.ShippingGiftRefundInNoor.Should().Be(0m);

        // پس از لغو جزئی، قیمت، تخفیف و ارسال سبد و مبلغ پرداخت قلم دوباره محاسبه نمی‌شوند.
        var unchangedCart = await Store.FindAsync(cart.Id, _buyer.Id.Value, default);
        unchangedCart!.ShippingAmountInNoor.Should().Be(30m);
        unchangedCart.ItemDiscountInNoor.Should().Be(20m);
        unchangedCart.Total.Should().Be(130m);
        (await PurchaseAsync(cart.Id, "book")).PaidAmountInNoor.Should().Be(100m);
        _pricing.Verify(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        var last = await AdminCancelAsync(purchase.Id.Value, 1);
        last.Items.Single().Should().Match<PhysicalCancellationItemResponse>(x =>
            x.RegularRefundInNoor == 24m && x.GiftRefundInNoor == 10m);
        last.ShippingRegularRefundInNoor.Should().Be(20m);
        last.ShippingGiftRefundInNoor.Should().Be(10m);
        last.TotalRefundInNoor.Should().Be(64m);

        var refunded = await PurchaseAsync(cart.Id, "book");
        refunded.CancelledQuantity.Should().Be(3);
        refunded.RefundedRegularInNoor.Should().Be(70m);
        refunded.RefundedGiftInNoor.Should().Be(30m);
        (await BalanceAsync()).Should().Be((1000m, 40m));
    }

    [CartPostgresFact]
    public async Task RefundLedger_ReversesOriginalPurchaseReferences_UnderOperationTransaction()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 30m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "book");
        var operationId = Guid.NewGuid();

        var result = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(nameof(PhysicalCancellationOutcome.Refunded));
        result.Value.Actor.Should().Be(nameof(PhysicalCancellationActor.Buyer));
        result.Value.TotalRefundInNoor.Should().Be(130m);

        var ledger = await LedgerAsync();
        var refunds = ledger.Where(x => x.ReferenceType == LedgerReferenceType.Refund).ToList();
        refunds.Should().OnlyContain(x => x.TransactionId == operationId);
        refunds.Sum(x => x.Amount).Should().Be(0m);
        refunds.Where(x => x.Amount > 0 && x.ReferenceId == purchase.Id.ToString())
            .Select(x => (x.BalanceType, x.Amount))
            .Should().BeEquivalentTo([(LedgerBalanceType.Regular, 70m), (LedgerBalanceType.Gift, 30m)]);
        refunds.Where(x => x.Amount > 0 && x.ReferenceId == cart.Id.ToString())
            .Select(x => (x.BalanceType, x.Amount))
            .Should().BeEquivalentTo([(LedgerBalanceType.Regular, 20m), (LedgerBalanceType.Gift, 10m)]);

        // هر برگشت دقیقاً قرینهٔ بدهی خرید با همان مرجع است.
        foreach (var reference in new[] { purchase.Id.ToString(), cart.Id.ToString() })
        {
            var debit = ledger.Where(x => x.ReferenceType == LedgerReferenceType.Purchase &&
                x.ReferenceId == reference && x.Amount < 0).Sum(x => x.Amount);
            var credit = refunds.Where(x => x.ReferenceId == reference && x.Amount > 0).Sum(x => x.Amount);
            credit.Should().Be(-debit);
        }

        ledger.Sum(x => x.Amount).Should().Be(0m);
        (await BalanceAsync()).Should().Be((1000m, 40m));
    }

    [CartPostgresFact]
    public async Task RefundCap_RejectsCancellationBeyondPaidAmount_InApplicationAndDatabase()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "book");

        (await AdminCancelAsync(purchase.Id.Value, 3)).TotalRefundInNoor.Should().Be(100m);
        var balance = await BalanceAsync();

        var again = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(purchase.Id.Value, 1, Guid.NewGuid(), Reason));
        again.Status.Should().Be(ResultStatus.Conflict);
        again.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.Conflict);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var overRefund = () => db.Set<PhysicalProductPurchase>()
            .Where(x => x.Id == purchase.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RefundedRegularInNoor, x => x.RefundedRegularInNoor + 1m));
        (await CaptureAsync(overRefund)).ConstraintName.Should().Be("CK_PhysicalProductPurchases_RefundCap");

        (await BalanceAsync()).Should().Be(balance);
    }

    [CartPostgresFact]
    public async Task RepeatedOperationId_ReturnsSameResultWithoutSecondRefund_AndMismatchedReuseConflicts()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 30m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "book");
        var operationId = Guid.NewGuid();

        var first = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));
        var replay = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));

        replay.IsSuccess.Should().BeTrue();
        replay.Value.Should().BeEquivalentTo(first.Value);
        (await BalanceAsync()).Should().Be((1000m, 40m));
        (await CancellationRowsAsync()).Should().Be(1);

        var reused = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(purchase.Id.Value, 1, operationId, Reason));
        reused.Status.Should().Be(ResultStatus.Conflict);
        reused.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.Conflict);
    }

    [CartPostgresFact]
    public async Task ConcurrentRequests_RefundOnlyOnce()
    {
        var sameIdCart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 30m, coupon: null);
        var operationId = Guid.NewGuid();

        var sameId = await Task.WhenAll(
            SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(sameIdCart.Id, operationId, Reason)),
            SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(sameIdCart.Id, operationId, Reason)));

        sameId.Should().OnlyContain(x => x.IsSuccess);
        sameId[0].Value.Should().BeEquivalentTo(sameId[1].Value);
        (await BalanceAsync()).Should().Be((1000m, 40m));

        var otherCart = await PaidCartAsync([new Line("pen", "خودکار", 50m, 0m, 1)], shipping: 0m, coupon: null);
        var balanceBefore = await BalanceAsync();

        var differentIds = await Task.WhenAll(
            SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(otherCart.Id, Guid.NewGuid(), Reason)),
            SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(otherCart.Id, Guid.NewGuid(), Reason)));

        differentIds.Count(x => x.IsSuccess).Should().Be(1);
        differentIds.Single(x => x.IsFailure).ErrorCode.Should().Be(PhysicalCancellationErrorCodes.Conflict);
        var after = await BalanceAsync();
        (after.Regular + after.Gift).Should().Be(balanceBefore.Regular + balanceBefore.Gift + 50m);
        (await CancellationRowsAsync()).Should().Be(2);
    }

    [CartPostgresFact]
    public async Task PaidCartCancellation_DoesNotReleaseConsumedCoupon_AndPreviewWarnsBeforeCancel()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 60m, 10m, 1)], shipping: 0m, coupon: "OFF");

        var preview = await SendAsync(_buyer.Id.Value, new GetMyPhysicalCancellationPreviewQuery(cart.Id));
        preview.IsSuccess.Should().BeTrue();
        preview.Value.TotalRefundInNoor.Should().Be(50m);
        preview.Value.Warnings.Select(x => x.Code).Should().BeEquivalentTo(
            [PhysicalCancellationWarningCodes.CouponRemoved, PhysicalCancellationWarningCodes.FreeShippingRemoved]);
        preview.Value.Warnings.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Message));
        (await CancellationRowsAsync()).Should().Be(0);

        var result = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRefundInNoor.Should().Be(50m);
        _pricing.Verify(x => x.TransitionAsync(
            It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()), Times.Never);
        (await Store.FindAsync(cart.Id, _buyer.Id.Value, default))!.CouponDiscountInNoor.Should().Be(10m);
    }

    [CartPostgresFact]
    public async Task UnpaidCartCancellation_ReleasesReservationWithoutRefund()
    {
        var cart = await ReadyCartAsync([new Line("book", "کتاب فیزیکی", 60m, 10m, 1)], shipping: 20m, coupon: "OFF");
        var balance = await BalanceAsync();

        var result = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(nameof(PhysicalCancellationOutcome.ReservationReleased));
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalRefundInNoor.Should().Be(0m);

        var released = await Store.FindAsync(cart.Id, _buyer.Id.Value, default);
        released!.Status.Should().Be(CartStatus.Failed);
        released.Synchronized.Should().BeTrue();
        _pricing.Verify(x => x.TransitionAsync(
            It.Is<CartTransitionRequest>(r => r.CartId == cart.ReservationId), "release", It.IsAny<CancellationToken>()),
            Times.Once);
        (await BalanceAsync()).Should().Be(balance);
        (await CancellationRowsAsync()).Should().Be(0);

        // آزادسازی رزرو پرداخت‌نشده برگشت موجودی نمی‌خواهد.
        _stock.Verify(x => x.RestockAsync(It.IsAny<PhysicalRestockRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [CartPostgresFact]
    public async Task Restock_SendsCancelledQuantitiesUnderOperationId_AfterRefundCommit()
    {
        var cart = await PaidCartAsync(
            [new Line("book", "کتاب فیزیکی", 40m, 20m, 3), new Line("pen", "خودکار", 50m, 0m, 2)],
            shipping: 0m, coupon: null);
        var book = await PurchaseAsync(cart.Id, "book");
        var pen = await PurchaseAsync(cart.Id, "pen");

        var adminOperation = Guid.NewGuid();
        var byAdmin = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(book.Id.Value, 2, adminOperation, Reason));
        byAdmin.IsSuccess.Should().BeTrue();

        var buyerOperation = Guid.NewGuid();
        var byBuyer = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, buyerOperation, Reason));
        byBuyer.IsSuccess.Should().BeTrue();

        // شناسهٔ سبد استرپی همان شناسهٔ رزرو commit است و هر قلم با تعداد لغوشدهٔ همین عملیات می‌رود.
        _restocks.Should().HaveCount(2);
        _restocks[0].Should().BeEquivalentTo(new PhysicalRestockRequest(
            cart.ReservationId, adminOperation, [new PhysicalRestockItem(book.Id.Value, 2)]));
        _restocks[1].StrapiCartId.Should().Be(cart.ReservationId);
        _restocks[1].OperationId.Should().Be(buyerOperation);
        _restocks[1].Items.Should().BeEquivalentTo(
            [new PhysicalRestockItem(book.Id.Value, 1), new PhysicalRestockItem(pen.Id.Value, 2)]);
        (await PendingRestockRowsAsync()).Should().Be(0);
    }

    [CartPostgresFact]
    public async Task Restock_IsNotRepeatedOnReplayAfterSuccess()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);
        var operationId = Guid.NewGuid();

        await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));
        await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));
        await RetryPendingRestocksAsync();

        _restocks.Should().ContainSingle(x => x.OperationId == operationId);
        (await BalanceAsync()).Should().Be((1000m, 40m));
    }

    [CartPostgresFact]
    public async Task StrapiRestockFailure_KeepsRefund_AndIsRetriedByReplayAndRecovery()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);
        var book = await PurchaseAsync(cart.Id, "book");
        StockClientReturns(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));

        var operationId = Guid.NewGuid();
        var first = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));

        // قطعی استرپی برگشت نور را برنمی‌گرداند؛ عملیات با کد خطا در صف می‌ماند.
        first.IsSuccess.Should().BeTrue();
        first.Value.TotalRefundInNoor.Should().Be(100m);
        var balance = await BalanceAsync();
        balance.Should().Be((1000m, 40m));
        (await StockReturnStateAsync(operationId)).Should().Be((null, CartErrorCodes.Unavailable));

        // تکرار درخواست لغو با همان شناسه نتیجه را بازپخش و برگشت موجودی ناموفق را دوباره امتحان می‌کند.
        StockClientReturns(Result.Success());
        var replay = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));

        replay.Value.Should().BeEquivalentTo(first.Value);
        var state = await StockReturnStateAsync(operationId);
        state.ReturnedAtUtc.Should().NotBeNull();
        state.ErrorCode.Should().BeNull();
        _restocks.Where(x => x.OperationId == operationId).Should().HaveCount(2)
            .And.OnlyContain(x => x.Items.Single() == new PhysicalRestockItem(book.Id.Value, 3));
        (await BalanceAsync()).Should().Be(balance);

        // مسیر بازیابی پس‌زمینه: عملیات ناموفق دیگر بدون تکرار کاربر تکمیل می‌شود.
        var otherCart = await PaidCartAsync([new Line("pen", "خودکار", 50m, 0m, 1)], shipping: 0m, coupon: null);
        StockClientReturns(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));
        var otherOperation = Guid.NewGuid();
        await SendAsync(_admin.Id.Value, new AdminCancelPhysicalPurchaseCommand(
            (await PurchaseAsync(otherCart.Id, "pen")).Id.Value, 1, otherOperation, Reason));
        (await PendingRestockRowsAsync()).Should().Be(1);

        StockClientReturns(Result.Success());
        await RetryPendingRestocksAsync();
        await RetryPendingRestocksAsync();

        (await PendingRestockRowsAsync()).Should().Be(0);
        _restocks.Count(x => x.OperationId == otherOperation).Should().Be(2);
        _restocks.Count(x => x.OperationId == operationId).Should().Be(2);
    }

    [CartPostgresFact]
    public async Task PermanentStrapiConflict_StopsBackgroundRetryAtMaxAttempts_AndStaysListedForAdmin()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);
        var book = await PurchaseAsync(cart.Id, "book");
        StockClientReturns(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));

        var operationId = Guid.NewGuid();
        await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, operationId, Reason));
        _restocks.Count(x => x.OperationId == operationId).Should().Be(1);

        // تلاش اول همان لغو بود؛ بازیابی فقط تا سقف ادامه می‌دهد و بعد از آن دیگر استرپی را صدا نمی‌زند.
        for (var i = 0; i < RestockMaxAttempts + 3; i++)
        {
            await RetryPendingRestocksAsync();
        }

        _restocks.Count(x => x.OperationId == operationId).Should().Be(RestockMaxAttempts);
        var stored = await StockReturnAttemptsAsync(operationId);
        stored.Should().Be(RestockMaxAttempts);

        var listed = (await PendingRestocksForAdminAsync()).Should().ContainSingle(x => x.OperationId == operationId).Subject;
        listed.Abandoned.Should().BeTrue();
        listed.Attempts.Should().Be(RestockMaxAttempts);
        listed.MaxAttempts.Should().Be(RestockMaxAttempts);
        listed.LastErrorCode.Should().Be(CartErrorCodes.Conflict);
        listed.CartId.Should().Be(cart.Id);
        listed.Items.Should().BeEquivalentTo([new PhysicalRestockItem(book.Id.Value, 3)]);
    }

    [CartPostgresFact]
    public async Task AdminPendingRestockList_ShowsUnfinishedOnly_AndMarksBelowCapAsNotAbandoned()
    {
        var failing = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);
        StockClientReturns(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        var failingOperation = Guid.NewGuid();
        await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(failing.Id, failingOperation, Reason));

        var succeeding = await PaidCartAsync([new Line("pen", "خودکار", 50m, 0m, 1)], shipping: 0m, coupon: null);
        StockClientReturns(Result.Success());
        var doneOperation = Guid.NewGuid();
        await SendAsync(_admin.Id.Value, new AdminCancelPhysicalPurchaseCommand(
            (await PurchaseAsync(succeeding.Id, "pen")).Id.Value, 1, doneOperation, Reason));

        var listed = await PendingRestocksForAdminAsync();

        var row = listed.Should().ContainSingle().Subject;
        row.OperationId.Should().Be(failingOperation);
        row.Attempts.Should().Be(0, "قطعی موقت استرپی از سقف تلاش کم نمی‌کند");
        row.Abandoned.Should().BeFalse();
        row.LastErrorCode.Should().Be(CartErrorCodes.Unavailable);
        row.LastAttemptAtUtc.Should().NotBeNull();
    }

    [CartPostgresFact]
    public void PendingRestockEndpoint_IsProtectedByNoorInjectPolicy()
    {
        var method = typeof(AdminPhysicalCancellationsController)
            .GetMethod(nameof(AdminPhysicalCancellationsController.PendingRestocks));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
        method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().BeEmpty("مجوز از سطح کنترلر (RequireNoorInject) ارث می‌رسد");
        PolicyOf(typeof(AdminPhysicalCancellationsController)).Should().Be(PolicyNames.RequireNoorInject);
    }

    [CartPostgresFact]
    public async Task Shipping_IsRefundedOnlyWhenWholeShipmentIsCancelledBeforeDispatch()
    {
        var cart = await PaidCartAsync(
            [new Line("item-a", "کتاب الف", 50m, 0m, 1), new Line("item-b", "کتاب ب", 50m, 0m, 1)],
            shipping: 20m, coupon: null);
        var itemA = await PurchaseAsync(cart.Id, "item-a");
        var itemB = await PurchaseAsync(cart.Id, "item-b");

        var preview = await SendAsync(_admin.Id.Value, new GetAdminPhysicalCancellationPreviewQuery(itemA.Id.Value, 1));
        preview.Value.ShippingRegularRefundInNoor.Should().Be(0m);
        preview.Value.Warnings.Select(x => x.Code).Should().Contain(PhysicalCancellationWarningCodes.ShippingNotRefunded);

        var partial = await AdminCancelAsync(itemA.Id.Value, 1);
        partial.TotalRefundInNoor.Should().Be(50m);
        (partial.ShippingRegularRefundInNoor + partial.ShippingGiftRefundInNoor).Should().Be(0m);

        // کاربر باقی‌ماندهٔ مرسوله را پیش از ارسال لغو می‌کند؛ کل مرسوله لغو شده و ارسال یک بار برمی‌گردد.
        var rest = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));
        rest.IsSuccess.Should().BeTrue();
        rest.Value.Items.Single().PurchaseId.Should().Be(itemB.Id.Value);
        (rest.Value.ShippingRegularRefundInNoor + rest.Value.ShippingGiftRefundInNoor).Should().Be(20m);

        var shippedCart = await PaidCartAsync(
            [new Line("item-c", "کتاب ج", 50m, 0m, 1), new Line("item-d", "کتاب د", 50m, 0m, 1)],
            shipping: 20m, coupon: null);
        var itemC = await PurchaseAsync(shippedCart.Id, "item-c");
        var itemD = await PurchaseAsync(shippedCart.Id, "item-d");
        await MarkShippedAsync(itemC.Id);
        var balance = await BalanceAsync();

        var byBuyer = await SendAsync(_buyer.Id.Value,
            new CancelMyPhysicalOrderCommand(shippedCart.Id, Guid.NewGuid(), Reason));
        var byAdmin = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(itemC.Id.Value, 1, Guid.NewGuid(), Reason));

        byBuyer.Status.Should().Be(ResultStatus.Conflict);
        byBuyer.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.AlreadyShipped);
        byAdmin.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.AlreadyShipped);
        (await BalanceAsync()).Should().Be(balance);

        // قلم ارسال‌نشدهٔ همان سبد با ادمین لغو می‌شود، ولی ارسال برنمی‌گردد.
        var unshipped = await AdminCancelAsync(itemD.Id.Value, 1);
        unshipped.TotalRefundInNoor.Should().Be(50m);
    }

    [CartPostgresFact]
    public async Task BuyerAndAdminAuthority_AreSeparated()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 0m, coupon: null);

        var foreign = await SendAsync(_otherUser.Id.Value,
            new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));
        var foreignPreview = await SendAsync(_otherUser.Id.Value, new GetMyPhysicalCancellationPreviewQuery(cart.Id));
        foreign.Status.Should().Be(ResultStatus.NotFound);
        foreign.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.NotFound);
        foreignPreview.Status.Should().Be(ResultStatus.NotFound);

        var missing = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(Guid.NewGuid(), 1, Guid.NewGuid(), Reason));
        missing.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.NotFound);

        var invalidQuantity = () => SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(Guid.NewGuid(), 0, Guid.NewGuid(), Reason));
        await invalidQuantity.Should().ThrowAsync<ValidationException>();

        // مسیر کاربر فقط JWT می‌خواهد و تعداد یا قلم نمی‌گیرد؛ لغو قلم/تعداد فقط با مجوز ادمین است.
        typeof(CancelMyPhysicalOrderCommand).GetProperties().Select(x => x.Name)
            .Should().BeEquivalentTo(["CartId", "OperationId", "Reason"]);
        PolicyOf(typeof(MyPhysicalOrdersController)).Should().BeNull();
        PolicyOf(typeof(AdminPhysicalCancellationsController)).Should().Be(PolicyNames.RequireNoorInject);
        (await BalanceAsync()).Should().Be((940m, 0m));
    }

    [CartPostgresFact]
    public async Task PurchasesWithoutPaymentShares_RequireManualReview()
    {
        var cart = await PaidCartAsync([new Line("book", "کتاب فیزیکی", 40m, 20m, 3)], shipping: 30m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "book");

        // شبیه‌سازی خرید پیش از migration سهم پرداخت (AddPaymentSharesAndTopUpCartLink).
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Set<PhysicalProductPurchase>()
                .Where(x => x.Id == purchase.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.RegularAmountInNoor, (decimal?)null)
                    .SetProperty(x => x.GiftAmountInNoor, (decimal?)null));
        }

        var balance = await BalanceAsync();
        var byBuyer = await SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));
        var byAdmin = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(purchase.Id.Value, 1, Guid.NewGuid(), Reason));

        byBuyer.Status.Should().Be(ResultStatus.Conflict);
        byBuyer.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.ManualReview);
        byAdmin.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.ManualReview);
        (await BalanceAsync()).Should().Be(balance);
        (await CancellationRowsAsync()).Should().Be(0);
        (await PurchaseAsync(cart.Id, "book")).RegularAmountInNoor.Should().BeNull();
    }

    [CartPostgresFact]
    public async Task DeliveryStatusUpdate_WaitsForBuyerAndCartLocksHeldByCancellation()
    {
        var cart = await PaidCartAsync([new Line("locked-book", "کتاب قفل", 20m, 0m, 1)], shipping: 0m, coupon: null);
        var purchase = await PurchaseAsync(cart.Id, "locked-book");

        // شبیه‌سازی لغوِ در جریان: همان دو قفل خریدار و سبد که PhysicalCancellationStore.ExecuteAsync می‌گیرد.
        await using var holder = new NpgsqlConnection(_adminConnection);
        await holder.OpenAsync();
        await using var holderTx = await holder.BeginTransactionAsync();
        await HoldAdvisoryLockAsync(holder, holderTx, BuyerLockSpace, _buyer.Id.Value);
        await HoldAdvisoryLockAsync(holder, holderTx, CartLockSpace, cart.Id);

        var shipping = SendDeliveryStatusAsync(purchase.Id.Value, nameof(PhysicalDeliveryStatus.Shipped));
        var finished = await Task.WhenAny(shipping, Task.Delay(TimeSpan.FromSeconds(2)));

        finished.Should().NotBe(shipping, "تغییر وضعیت باید پشت قفل سبدِ در حال لغو منتظر بماند");

        await holderTx.CommitAsync();
        var response = await shipping;
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
    }

    [CartPostgresFact]
    public async Task ConcurrentCancellationAndShipping_NeverLeaveShippedPurchaseWithRefund()
    {
        const int attempts = 12;
        for (var i = 0; i < attempts; i++)
        {
            var cart = await PaidCartAsync([new Line($"race-{i}", "کتاب رقابت", 20m, 0m, 1)], shipping: 0m, coupon: null);
            var purchase = await PurchaseAsync(cart.Id, $"race-{i}");

            var cancel = SendAsync(_buyer.Id.Value, new CancelMyPhysicalOrderCommand(cart.Id, Guid.NewGuid(), Reason));
            var ship = TrySendDeliveryStatusAsync(purchase.Id.Value, nameof(PhysicalDeliveryStatus.Shipped));
            await Task.WhenAll(cancel, ship);

            // لغو پس از ارسال یا ارسال پس از لغو مجاز نیست؛ فقط یکی از دو عملیات می‌تواند اثر بگذارد.
            var stored = await PurchaseAsync(cart.Id, $"race-{i}");
            var shippedWithRefund = stored.DeliveryStatus == PhysicalDeliveryStatus.Shipped && stored.CancelledQuantity > 0;
            shippedWithRefund.Should().BeFalse($"attempt {i}: ارسال و لغو نباید هم‌زمان موفق شوند");
            (cancel.Result.IsSuccess && ship.Result).Should().BeFalse($"attempt {i}");
        }
    }

    private sealed record Line(string Id, string Title, decimal UnitPrice, decimal Discount, int Quantity);

    // مقادیر قفل‌های PurchaseAdvisoryLocks (internal در Infrastructure)؛ باید با آن کلاس یکی بمانند.
    private const int CartLockSpace = 1869767797;
    private const int BuyerLockSpace = 1869767798;

    private static async Task HoldAdvisoryLockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int lockSpace, Guid key)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@space, hashtext(@key))", connection, transaction);
        command.Parameters.AddWithValue("space", lockSpace);
        command.Parameters.AddWithValue("key", key.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task<PhysicalDeliveryStatusResponse> SendDeliveryStatusAsync(Guid purchaseId, string status)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new UpdatePhysicalDeliveryStatusCommand(purchaseId, status));
    }

    private async Task<bool> TrySendDeliveryStatusAsync(Guid purchaseId, string status)
    {
        try
        {
            await SendDeliveryStatusAsync(purchaseId, status);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private void StockClientReturns(Result result)
    {
        _stock
            .Setup(x => x.RestockAsync(It.IsAny<PhysicalRestockRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PhysicalRestockRequest, CancellationToken>((request, _) =>
            {
                lock (_restocks)
                {
                    _restocks.Add(request);
                }
            })
            .ReturnsAsync(result);
    }

    private async Task RetryPendingRestocksAsync()
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PhysicalRestockService>().RetryPendingAsync(50, default);
    }

    private const int RestockMaxAttempts = 3;

    private async Task<IReadOnlyList<PendingPhysicalRestockResponse>> PendingRestocksForAdminAsync()
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetAdminPendingPhysicalRestocksQuery());
    }

    private async Task<int> StockReturnAttemptsAsync(Guid operationId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<PhysicalProductCancellation>()
            .Where(x => x.OperationId == operationId)
            .MaxAsync(x => x.StockReturnAttempts);
    }

    private async Task<int> PendingRestockRowsAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<PhysicalProductCancellation>().CountAsync(x => x.StockReturnedAtUtc == null);
    }

    private async Task<(DateTime? ReturnedAtUtc, string? ErrorCode)> StockReturnStateAsync(Guid operationId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Set<PhysicalProductCancellation>().AsNoTracking().SingleAsync(x => x.OperationId == operationId);
        return (row.StockReturnedAtUtc, row.StockReturnErrorCode);
    }

    private async Task<Cart> ReadyCartAsync(Line[] lines, decimal shipping, string? coupon)
    {
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId,
            lines.Select(x => new CartItemInput(x.Id, ProductItemType.Product, x.Quantity)).ToArray(), coupon);
        (await Store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var quote = lines
            .Select(x => new CartQuoteItem(
                x.Id,
                x.Title,
                x.UnitPrice,
                coupon is null ? x.Discount : 0m,
                coupon is null ? 0m : x.Discount,
                x.UnitPrice * x.Quantity - x.Discount,
                ProductItemType.Product,
                null,
                x.Quantity))
            .ToArray();

        return await Store.SetQuoteAsync(cart.Id, _buyer.Id.Value, quote, shipping, null, cart.ReservationId, default);
    }

    private async Task<Cart> PaidCartAsync(Line[] lines, decimal shipping, string? coupon)
    {
        var cart = await ReadyCartAsync(lines, shipping, coupon);
        (await Store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        return cart;
    }

    private async Task<PhysicalCancellationResponse> AdminCancelAsync(Guid purchaseId, int quantity)
    {
        var result = await SendAsync(_admin.Id.Value,
            new AdminCancelPhysicalPurchaseCommand(purchaseId, quantity, Guid.NewGuid(), "درخواست پشتیبانی"));
        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.Value.Actor.Should().Be(nameof(PhysicalCancellationActor.Admin));
        return result.Value;
    }

    private async Task<Result<T>> SendAsync<T>(Guid userId, IRequest<Result<T>> request)
    {
        _currentUser.UserId = userId;
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<PhysicalProductPurchase> PurchaseAsync(Guid cartId, string externalId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .SingleAsync(x => x.CartId == cartId && x.ExternalProductId == externalId);
    }

    private async Task MarkShippedAsync(PhysicalProductPurchaseId id)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purchase = await db.Set<PhysicalProductPurchase>().SingleAsync(x => x.Id == id);
        purchase.MarkShipped();
        await db.SaveChangesAsync();
    }

    private async Task<(decimal Regular, decimal Gift)> BalanceAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().AsNoTracking().SingleAsync(x => x.UserId == _buyer.Id);
        return (wallet.WithdrawableBalance, wallet.GiftBalance);
    }

    private async Task<List<LedgerEntry>> LedgerAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<LedgerEntry>().AsNoTracking().ToListAsync();
    }

    private async Task<int> CancellationRowsAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<PhysicalProductCancellation>().CountAsync();
    }

    private static async Task<PostgresException> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is PostgresException postgres)
                {
                    return postgres;
                }
            }

            throw;
        }

        throw new InvalidOperationException("Database constraint was expected to reject the update.");
    }

    private static string? PolicyOf(Type controller) =>
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; }
    }
}
