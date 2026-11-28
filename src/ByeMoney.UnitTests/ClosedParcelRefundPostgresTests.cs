using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;
using ByeMoney.Application.Modules.Purchases.Commands.AdminRefundClosedParcel;
using ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;
using ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;
using ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierAlert;
using ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Application.Modules.Purchases.Delivery.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrderReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrderReceipt;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace ByeMoney.UnitTests;

public sealed class ClosedParcelRefundPostgresTests : IAsyncLifetime
{
    private const string RefundReason = "برگشت وجه مرسولهٔ مفقود/برگشتی بسته‌شده بدون تسویه";

    private readonly string _schema = "closed_parcel_refund_test_" + Guid.NewGuid().ToString("N");
    private readonly User _buyer = User.CreateInitial("cpr-buyer-" + Guid.NewGuid().ToString("N"));
    private readonly User _admin = User.CreateInitial("cpr-admin-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICartPricingClient> _pricing = new();
    private readonly Mock<IPhysicalStockClient> _stock = new();
    private readonly Mock<ITrackingProviderClient> _tracking = new();
    private readonly TestCurrentUser _currentUser = new();
    private ServiceProvider _provider = null!;
    private string _adminConnection = null!;

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
        services.AddSingleton(new PhysicalRestockSettings { MaxAttempts = 3 });
        services.AddSingleton(new PhysicalDeliverySettings { AutoConfirmAfterDays = 30 });
        services.AddSingleton(_pricing.Object);
        services.AddSingleton(_stock.Object);
        services.AddSingleton(_tracking.Object);
        services.AddSingleton<ICurrentUserService>(_currentUser);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.AddRange(_buyer, _admin);
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        var wallet = await provisioning.GetOrCreateUserWalletAsync(_buyer.Id, FinancialScope.TarheElahi);
        wallet.Wallet.ApplyCredit(1000m);
        wallet.Wallet.ApplyGiftCredit(40m);
        await provisioning.GetOrCreateSystemAccountAsync(FinancialScope.TarheElahi);
        await db.SaveChangesAsync();

        _pricing
            .Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _stock
            .Setup(x => x.RestockAsync(It.IsAny<PhysicalRestockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _tracking.SetupGet(x => x.Provider).Returns(PhysicalProductPurchase.PostexProvider);
        _tracking.SetupGet(x => x.IsConfigured).Returns(true);
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
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    [CartPostgresFact]
    public async Task ClosedWithoutSettlement_RefundsFullMixedShares_PreservesZeroSumLedger_AndSkipsRestockAndDeliveryFlip()
    {
        var cart = await PaidCartAsync([new Line("book-1", "کتاب ۱", 100m, 0m, 1)], shipping: 30m);
        var purchase = await PurchaseAsync(cart.Id, "book-1");
        // پرداخت: ۴۰ هدیه بین قلم (۳۰) و ارسال (۱۰) تقسیم می‌شود؛ پس قلم ۶۰ عادی + ۴۰ هدیه یا ۷۰ عادی + ۳۰ هدیه
        purchase.RegularAmountInNoor.Should().Be(70m);
        purchase.GiftAmountInNoor.Should().Be(30m);
        (await BalanceAsync()).Should().Be((910m, 0m));

        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        var opId = Guid.NewGuid();
        var result = await RefundClosedParcelAsync(purchase.Id.Value, opId, RefundReason);
        result.IsSuccess.Should().BeTrue();
        result.Value.RegularRefundInNoor.Should().Be(70m);
        result.Value.GiftRefundInNoor.Should().Be(30m);
        result.Value.TotalRefundInNoor.Should().Be(100m);

        // موجودی کیف پول دقیقاً به اندازهٔ سهم قلم برمی‌گردد (کرایهٔ ۳۰ نوری برنمی‌گردد)
        (await BalanceAsync()).Should().Be((980m, 30m));

        // نامتغیر جمع صفر Ledger و حفظ ScopeId
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entries = await db.Set<LedgerEntry>().AsNoTracking()
            .Where(x => x.TransactionId == opId)
            .ToListAsync();
        entries.Should().HaveCount(4); // دو بدهکار سیستم + دو بستانکار کاربر
        entries.Sum(x => x.Amount).Should().Be(0m);
        var accountIds = entries.Select(x => x.AccountId).Distinct().ToArray();
        var scopes = await db.Set<ByeMoney.Domain.Modules.Wallet.Accounts.Account>().AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => a.ScopeId)
            .Distinct()
            .ToListAsync();
        scopes.Should().Equal(FinancialScope.TarheElahi);

        var updated = await PurchaseAsync(cart.Id, "book-1");
        updated.CancelledQuantity.Should().Be(0);
        updated.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        updated.HasClosedParcelRefund.Should().BeTrue();

        // هیچ ردیفی در PhysicalProductCancellations و هیچ restockای ساخته نمی‌شود
        (await db.Set<PhysicalProductCancellation>().CountAsync()).Should().Be(0);
        _stock.Verify(x => x.RestockAsync(It.IsAny<PhysicalRestockRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // سفارش همچنان از تسویه خارج است
        var candidates = scope.ServiceProvider.GetRequiredService<OrderSettlementCandidateService>();
        var unsettled = await candidates.GetUnsettledAsync(FinancialScope.TarheElahi, CancellationToken.None);
        unsettled.Should().NotContain(x => x.OrderId == OrderIdFormat.For(OrderSourceType.Cart, cart.Id));
    }

    [CartPostgresFact]
    public async Task IdempotentReplay_ReturnsSameResponse_AndMismatchReturnsConflict()
    {
        var cart = await PaidCartAsync([new Line("book-2", "کتاب ۲", 80m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-2");
        await ShipAndCloseWithoutSettlementByAlertAsync(purchase.Id.Value);

        var opId = Guid.NewGuid();
        var first = await RefundClosedParcelAsync(purchase.Id.Value, opId, RefundReason);
        var replay = await RefundClosedParcelAsync(purchase.Id.Value, opId, "  " + RefundReason + "  ");
        var mismatch = await RefundClosedParcelAsync(purchase.Id.Value, opId, "علت متفاوت");

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.Should().Be(first.Value);
        mismatch.IsFailure.Should().BeTrue();
        mismatch.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.Conflict);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<LedgerEntry>().CountAsync(x => x.TransactionId == opId)).Should().Be(4);
    }

    [CartPostgresFact]
    public async Task ConcurrentRequests_WithDifferentOperationIds_AllowOnlyOneRefund()
    {
        var cart = await PaidCartAsync([new Line("book-3", "کتاب ۳", 90m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-3");
        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        var t1 = Task.Run(() => RefundClosedParcelInNewScopeAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason));
        var t2 = Task.Run(() => RefundClosedParcelInNewScopeAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason));
        var results = await Task.WhenAll(t1, t2);

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.IsFailure &&
            (r.ErrorCode == ClosedParcelRefundErrorCodes.AlreadyRefunded ||
             r.ErrorCode == ClosedParcelRefundErrorCodes.Conflict)).Should().Be(1);
        (await BalanceAsync()).Should().Be((1000m, 40m));
    }

    [CartPostgresFact]
    public async Task PriorPartialQuantityRefund_RefundsExactRemainingRegularAndGiftShares()
    {
        // ۲ عدد × ۵۰ = ۱۰۰ نور (۶۰ عادی + ۴۰ هدیه)؛ ۱ عدد پیش از ارسال لغو می‌شود (۳۰ عادی + ۲۰ هدیه)
        var cart = await PaidCartAsync([new Line("book-4", "کتاب ۴", 50m, 0m, 2)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-4");

        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var cancel = await sender.Send(new AdminCancelPhysicalPurchaseCommand(
                purchase.Id.Value, 1, Guid.NewGuid(), "لغو جزئی پیش از ارسال"));
            cancel.IsSuccess.Should().BeTrue();
        }

        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        var refund = await RefundClosedParcelAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason);
        refund.IsSuccess.Should().BeTrue();
        refund.Value.RegularRefundInNoor.Should().Be(30m);
        refund.Value.GiftRefundInNoor.Should().Be(20m);
        refund.Value.TotalRefundInNoor.Should().Be(50m);
        (await BalanceAsync()).Should().Be((1000m, 40m));
    }

    [CartPostgresFact]
    public async Task AlreadySettledOrder_AndMissingShares_AreRejected_AndShowManualReviewInAdmin()
    {
        // ۱. سفارش تسویه‌شده
        var settledCart = await PaidCartAsync([new Line("book-s", "کتاب تسویه", 50m, 0m, 1)], shipping: 0m);
        var settledPurchase = await PurchaseAsync(settledCart.Id, "book-s");
        await ShipAndCloseWithoutSettlementByAlertAsync(settledPurchase.Id.Value);

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var period = OrderSettlementPeriod.Close(
                _admin.Id,
                "دوره تستی",
                null,
                [OrderSettlementItem.Create(
                    OrderIdFormat.For(OrderSourceType.Cart, settledCart.Id),
                    OrderSourceType.Cart.ToString(),
                    _buyer.ExternalUserId,
                    settledCart.CreatedAtUtc,
                    new PaymentShares(50m, 0m))],
                FinancialScope.TarheElahi);
            db.Add(period);
            await db.SaveChangesAsync();
        }

        var settledResult = await RefundClosedParcelAsync(settledPurchase.Id.Value, Guid.NewGuid(), RefundReason);
        settledResult.IsFailure.Should().BeTrue();
        settledResult.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.AlreadySettled);

        // ۲. خرید بدون سهم ثبت‌شده
        var noShareCart = await PaidCartAsync([new Line("book-n", "کتاب بدون سهم", 50m, 0m, 1)], shipping: 0m);
        var noSharePurchase = await PurchaseAsync(noShareCart.Id, "book-n");
        await ShipAndCloseWithoutSettlementByReturnAsync(noSharePurchase.Id.Value);

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Set<PhysicalProductPurchase>()
                .Where(x => x.Id == noSharePurchase.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.RegularAmountInNoor, (decimal?)null)
                    .SetProperty(x => x.GiftAmountInNoor, (decimal?)null));
        }

        var noShareResult = await RefundClosedParcelAsync(noSharePurchase.Id.Value, Guid.NewGuid(), RefundReason);
        noShareResult.IsFailure.Should().BeTrue();
        noShareResult.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.ManualReview);

        // بررسی نمایش ManualReview در پنل ادمین
        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var settledDetails = await sender.Send(new GetAdminOrderQuery(
                OrderIdFormat.For(OrderSourceType.Cart, settledCart.Id)));
            settledDetails.Order.Items.Single().ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.ManualReview);

            var noShareDetails = await sender.Send(new GetAdminOrderQuery(
                OrderIdFormat.For(OrderSourceType.Cart, noShareCart.Id)));
            noShareDetails.Order.Items.Single().ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.ManualReview);
        }
    }

    [CartPostgresFact]
    public async Task ZeroRemainingAmount_DoesNotShowNotRefunded_AndReturnsDistinctErrorCode()
    {
        // خرید رایگان (مبلغ صفر) که ClosedWithoutSettlement شده است
        var cart = await PaidCartAsync([new Line("book-free", "کتاب رایگان", 0m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-free");
        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var details = await sender.Send(new GetAdminOrderQuery(
                OrderIdFormat.For(OrderSourceType.Cart, cart.Id)));
            details.Order.Items.Single().ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.None);
        }

        var refund = await RefundClosedParcelAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason);
        refund.IsFailure.Should().BeTrue();
        refund.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.NoRemainingAmount);
        refund.ErrorCode.Should().NotBe(ClosedParcelRefundErrorCodes.AlreadyRefunded);
    }

    [CartPostgresFact]
    public async Task AfterRefund_BlocksSecondPathResolution_BuyerConfirm_AutoConfirm_AndTrackingUpdate()
    {
        var cart = await PaidCartAsync([new Line("book-lock", "کتاب قفل‌شده", 60m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-lock");

        // ثبت هر دو پرچم برگشتی و هشدار؛ اول برگشتی با ClosedWithoutSettlement بسته می‌شود، هشدار باز می‌ماند -> فعلاً رد
        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new UpdatePhysicalDeliveryStatusCommand(
                purchase.Id.Value, nameof(PhysicalDeliveryStatus.Shipped), "1234567890", PhysicalProductPurchase.PostexProvider, "parcel-lock"));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entity = await db.Set<PhysicalProductPurchase>().SingleAsync(x => x.Id == purchase.Id);
            entity.MarkCarrierReturned(DateTime.UtcNow.AddDays(-35));
            entity.MarkCarrierAlert(PhysicalProductPurchase.DamagedParcelStatusCode, DateTime.UtcNow.AddDays(-35));
            await db.SaveChangesAsync();

            await sender.Send(new ResolveCarrierReturnCommand(
                purchase.Id.Value, nameof(CarrierReturnResolution.ClosedWithoutSettlement), "بسته بدون تسویه"));
        }

        var whileAlertOpen = await RefundClosedParcelAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason);
        whileAlertOpen.IsFailure.Should().BeTrue();
        whileAlertOpen.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.NotEligible);

        // بستن هشدار هم با ClosedWithoutSettlement -> حالا واجد شرایط است و برگشت انجام می‌شود
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ResolveCarrierAlertCommand(
                purchase.Id.Value, nameof(CarrierReturnResolution.ClosedWithoutSettlement), "هشدار هم بسته شد"));
        }

        var refund = await RefundClosedParcelAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason);
        refund.IsSuccess.Should().BeTrue();

        // ۱. بستن مسیر دوم یا تغییر تصمیم پس از برگشت رد می‌شود
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var actReturn = () => sender.Send(new ResolveCarrierReturnCommand(
                purchase.Id.Value, nameof(CarrierReturnResolution.DeliveryConfirmed), "تغییر پس از برگشت"));
            await actReturn.Should().ThrowAsync<ConflictDomainException>();

            var actAlert = () => sender.Send(new ResolveCarrierAlertCommand(
                purchase.Id.Value, nameof(CarrierReturnResolution.DeliveryConfirmed), "تغییر پس از برگشت"));
            await actAlert.Should().ThrowAsync<ConflictDomainException>();

            // ۲. تغییر وضعیت یا کد رهگیری توسط ادمین رد می‌شود
            var actShip = () => sender.Send(new UpdatePhysicalDeliveryStatusCommand(
                purchase.Id.Value, nameof(PhysicalDeliveryStatus.Shipped), "9999999999", PhysicalProductPurchase.ManualProvider));
            await actShip.Should().ThrowAsync<ConflictDomainException>();

            var actDeliver = () => sender.Send(new UpdatePhysicalDeliveryStatusCommand(
                purchase.Id.Value, nameof(PhysicalDeliveryStatus.Delivered)));
            await actDeliver.Should().ThrowAsync<ConflictDomainException>();
        }

        // ۳. تأیید خریدار پس از برگشت رد می‌شود
        _currentUser.Set(_buyer.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var actConfirm = () => sender.Send(new ConfirmMyPhysicalDeliveryCommand(
                OrderIdFormat.For(OrderSourceType.Cart, cart.Id)));
            await actConfirm.Should().ThrowAsync<ConflictDomainException>();
        }

        // ۴. تحویل خودکار و همگام‌سازی بیرونی قلم را Delivered نمی‌کند
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Set<PhysicalProductPurchase>()
                .Where(x => x.Id == purchase.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ShippedAtUtc, DateTime.UtcNow.AddDays(-40)));

            _tracking
                .Setup(x => x.GetTrackingAsync(It.IsAny<TrackingLookup>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TrackingResult(TrackingOutcome.Delivered));

            var sync = scope.ServiceProvider.GetRequiredService<PhysicalDeliverySyncService>();
            (await sync.ConfirmExpiredAsync(CancellationToken.None)).Should().Be(0);
            (await sync.SyncExternalAsync(CancellationToken.None)).Should().Be(0);
        }

        (await PurchaseAsync(cart.Id, "book-lock")).DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
    }

    [CartPostgresFact]
    public async Task OperationIdUniqueness_IsEnforcedBidirectionally_AcrossCancellationAndLedger()
    {
        var cart = await PaidCartAsync(
        [
            new Line("book-u1", "کتاب ۱", 40m, 0m, 1),
            new Line("book-u2", "کتاب ۲", 40m, 0m, 1)
        ], shipping: 0m);
        var p1 = await PurchaseAsync(cart.Id, "book-u1");
        var p2 = await PurchaseAsync(cart.Id, "book-u2");
        await ShipAndCloseWithoutSettlementByReturnAsync(p1.Id.Value);

        var opId = Guid.NewGuid();
        var refund = await RefundClosedParcelAsync(p1.Id.Value, opId, RefundReason);
        refund.IsSuccess.Should().BeTrue();

        // استفادهٔ مجدد همان operationId در لغو عادی باید رد شود
        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var reuseInCancel = await sender.Send(new AdminCancelPhysicalPurchaseCommand(
                p2.Id.Value, 1, opId, "تلاش استفاده مجدد شناسه"));
            reuseInCancel.IsFailure.Should().BeTrue();
            reuseInCancel.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.Conflict);
        }

        // استفادهٔ مجدد همان operationId در لغو رزرو (سبد پرداخت‌نشده) نیز باید رد شود
        var unpaidCart = await ReadyCartAsync([new Line("book-u3", "کتاب ۳", 40m, 0m, 1)], shipping: 0m);
        _currentUser.Set(_buyer.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var reuseInReservationCancel = await sender.Send(new CancelMyPhysicalOrderCommand(
                unpaidCart.Id, opId, "تلاش استفاده مجدد در لغو رزرو"));
            reuseInReservationCancel.IsFailure.Should().BeTrue();
            reuseInReservationCancel.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.Conflict);
        }

        // و برعکس: استفادهٔ شناسهٔ لغو عادی یا شناسهٔ تراکنش دفتر کل در برگشت جدید باید رد شود
        await ShipAndCloseWithoutSettlementByReturnAsync(p2.Id.Value);
        var cancelOpId = Guid.NewGuid();
        // استفاده از TransactionId خرید (که در LedgerEntries ثبت شده)
        var reuseLedgerTx = await RefundClosedParcelAsync(p2.Id.Value, cart.Id, RefundReason);
        reuseLedgerTx.IsFailure.Should().BeTrue();
        reuseLedgerTx.ErrorCode.Should().Be(ClosedParcelRefundErrorCodes.Conflict);

        _ = cancelOpId;
    }

    [CartPostgresFact]
    public async Task ConcurrentSettlementClose_AndClosedParcelRefund_CannotBothSucceed()
    {
        // سفارش آمادهٔ تسویه را در حالی که CloseOrderSettlementPeriod و ClosedParcelRefund هم‌زمان اجرا می‌شوند تست می‌کنیم
        var cart = await PaidCartAsync([new Line("book-race", "کتاب هم‌زمان", 50m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-race");
        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        var orderId = OrderIdFormat.For(OrderSourceType.Cart, cart.Id);
        var period = OrderSettlementPeriod.Close(
            _admin.Id,
            "دوره هم‌زمان",
            null,
            [OrderSettlementItem.Create(
                orderId,
                OrderSourceType.Cart.ToString(),
                _buyer.ExternalUserId,
                cart.CreatedAtUtc,
                new PaymentShares(50m, 0m))],
            FinancialScope.TarheElahi);

        var settleTask = Task.Run(async () =>
        {
            using var scope = _provider.CreateScope();
            var settlementStore = scope.ServiceProvider.GetRequiredService<IOrderSettlementStore>();
            return await settlementStore.TryAddAsync(period, CancellationToken.None);
        });
        var refundTask = Task.Run(() => RefundClosedParcelInNewScopeAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason));

        var settled = await settleTask;
        var refundResult = await refundTask;

        // Under LockCartAsync: TryAddAsync detects ClosedWithoutSettlement / ClosedParcelRefund and returns false,
        // while ClosedParcelRefund succeeds (or if TryAddAsync ran without blocker it would block refund).
        // In all cases, both CANNOT succeed simultaneously!
        (settled && refundResult.IsSuccess).Should().BeFalse();
        refundResult.IsSuccess.Should().BeTrue();
        settled.Should().BeFalse();
    }

    [CartPostgresFact]
    public async Task Visibility_IsConsistentAcrossAdminList_AdminDetails_AndBuyerDetails()
    {
        var cart = await PaidCartAsync([new Line("book-vis", "کتاب نمایش", 70m, 0m, 1)], shipping: 0m);
        var purchase = await PurchaseAsync(cart.Id, "book-vis");
        await ShipAndCloseWithoutSettlementByReturnAsync(purchase.Id.Value);

        var orderId = OrderIdFormat.For(OrderSourceType.Cart, cart.Id);

        // پیش از برگشت: ادمین NotRefunded می‌بیند، خریدار None
        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var adminBefore = await sender.Send(new GetAdminOrderQuery(orderId));
            adminBefore.Order.Items.Single().ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.NotRefunded);
        }

        _currentUser.Set(_buyer.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var buyerBefore = await sender.Send(new GetMyOrderQuery(orderId));
            buyerBefore.Items.Single().ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.None);
        }

        // ثبت برگشت
        var refund = await RefundClosedParcelAsync(purchase.Id.Value, Guid.NewGuid(), RefundReason);
        refund.IsSuccess.Should().BeTrue();

        // پس از برگشت: فهرست ادمین، جزئیات ادمین و جزئیات خریدار همگی هم‌ترازند و علت ادمین برای خریدار مخفی است
        _currentUser.Set(_admin.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var adminList = await sender.Send(new GetAdminOrdersQuery(new AdminOrderFilter(Page: 1, PageSize: 20)));
            adminList.Items.Single(x => x.OrderId == orderId).RefundedAmountInNoor.Should().Be(70m);

            var adminAfter = await sender.Send(new GetAdminOrderQuery(orderId));
            var item = adminAfter.Order.Items.Single();
            item.ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.Refunded);
            item.ClosedParcelRefundAmountInNoor.Should().Be(70m);
            item.ClosedParcelRefundedAtUtc.Should().NotBeNull();
            item.ClosedParcelRefundReason.Should().Be(RefundReason);
            adminAfter.Order.DeliveryStatus.Should().NotBe(OrderDeliveryStatus.Cancelled.ToString());

            var adminReceipt = await sender.Send(new GetAdminOrderReceiptQuery(orderId));
            adminReceipt.Items.Single().RefundedRegularInNoor.Should().Be(30m);
            adminReceipt.Items.Single().RefundedGiftInNoor.Should().Be(40m);
        }

        _currentUser.Set(_buyer.Id);
        using (var scope = _provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var buyerAfter = await sender.Send(new GetMyOrderQuery(orderId));
            var item = buyerAfter.Items.Single();
            item.ClosedParcelRefundStatus.Should().Be(ClosedParcelRefundStatuses.Refunded);
            item.ClosedParcelRefundAmountInNoor.Should().Be(70m);
            item.ClosedParcelRefundedAtUtc.Should().NotBeNull();
            item.ClosedParcelRefundReason.Should().BeNull();
            buyerAfter.DeliveryStatus.Should().NotBe(OrderDeliveryStatus.Cancelled.ToString());

            var buyerReceipt = await sender.Send(new GetMyOrderReceiptQuery(orderId));
            buyerReceipt.Items.Single().RefundedRegularInNoor.Should().Be(30m);
            buyerReceipt.Items.Single().RefundedGiftInNoor.Should().Be(40m);
        }
    }

    private sealed record Line(string ExternalId, string Title, decimal Price, decimal Discount, int Quantity);

    private async Task<Cart> ReadyCartAsync(Line[] lines, decimal shipping)
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICartStore>();
        var cart = Cart.Create(
            Guid.NewGuid(),
            _buyer.Id.Value,
            _buyer.ExternalUserId,
            lines.Select(x => new CartItemInput(x.ExternalId, ProductItemType.Product, x.Quantity)).ToArray(),
            null,
            FinancialScope.TarheElahi);
        (await store.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();

        var quote = lines
            .Select(x => new CartQuoteItem(
                x.ExternalId,
                x.Title,
                x.Price,
                x.Discount,
                0m,
                x.Price * x.Quantity - x.Discount,
                ProductItemType.Product,
                null,
                x.Quantity))
            .ToArray();

        await store.SetQuoteAsync(cart.Id, _buyer.Id.Value, quote, shipping, null, null, cart.ReservationId, default);
        return cart;
    }

    private async Task<Cart> PaidCartAsync(Line[] lines, decimal shipping)
    {
        var cart = await ReadyCartAsync(lines, shipping);
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICartStore>();
        (await store.PayAsync(cart.Id, _buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        return cart;
    }

    private async Task<PhysicalProductPurchase> PurchaseAsync(Guid cartId, string externalId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<PhysicalProductPurchase>().AsNoTracking()
            .SingleAsync(x => x.CartId == cartId && x.ExternalProductId == externalId);
    }

    private async Task<(decimal Regular, decimal Gift)> BalanceAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await db.Set<Wallet>().AsNoTracking()
            .SingleAsync(x => x.UserId == _buyer.Id && x.ScopeId == FinancialScope.TarheElahi);
        return (wallet.WithdrawableBalance, wallet.GiftBalance);
    }

    private async Task ShipAndCloseWithoutSettlementByReturnAsync(Guid purchaseId)
    {
        _currentUser.Set(_admin.Id);
        using var scope = _provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new UpdatePhysicalDeliveryStatusCommand(
            purchaseId, nameof(PhysicalDeliveryStatus.Shipped), "1234567890", PhysicalProductPurchase.ManualProvider));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Set<PhysicalProductPurchase>().SingleAsync(x => x.Id == new PhysicalProductPurchaseId(purchaseId));
        entity.MarkCarrierReturned(DateTime.UtcNow);
        await db.SaveChangesAsync();

        await sender.Send(new ResolveCarrierReturnCommand(
            purchaseId, nameof(CarrierReturnResolution.ClosedWithoutSettlement), "بستن بدون تسویه"));
    }

    private async Task ShipAndCloseWithoutSettlementByAlertAsync(Guid purchaseId)
    {
        _currentUser.Set(_admin.Id);
        using var scope = _provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new UpdatePhysicalDeliveryStatusCommand(
            purchaseId, nameof(PhysicalDeliveryStatus.Shipped), "1234567890", PhysicalProductPurchase.ManualProvider));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Set<PhysicalProductPurchase>().SingleAsync(x => x.Id == new PhysicalProductPurchaseId(purchaseId));
        entity.MarkCarrierAlert(PhysicalProductPurchase.MissingShipmentStatusCode, DateTime.UtcNow);
        await db.SaveChangesAsync();

        await sender.Send(new ResolveCarrierAlertCommand(
            purchaseId, nameof(CarrierReturnResolution.ClosedWithoutSettlement), "مفقودی بسته‌شده بدون تسویه"));
    }

    private async Task<Result<ClosedParcelRefundResponse>> RefundClosedParcelAsync(
        Guid purchaseId, Guid operationId, string reason)
    {
        _currentUser.Set(_admin.Id);
        return await RefundClosedParcelInNewScopeAsync(purchaseId, operationId, reason);
    }

    private async Task<Result<ClosedParcelRefundResponse>> RefundClosedParcelInNewScopeAsync(
        Guid purchaseId, Guid operationId, string reason)
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClosedParcelRefundService>();
        return await service.RefundAsync(_admin.Id.Value, purchaseId, operationId, reason, CancellationToken.None);
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        private Guid _userId;
        public void Set(UserId id) => _userId = id.Value;
        public Guid? UserId => _userId == Guid.Empty ? null : _userId;
        public string? ExternalUserId => null;
        public bool IsAuthenticated => _userId != Guid.Empty;
        public bool HasPermission(string permission) => true;
    }
}
