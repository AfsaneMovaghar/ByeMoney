using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ByeMoney.UnitTests;

public sealed class PurchaseHistoryPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "purchase_history_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;

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
            .AddEnvironmentVariables().Build();
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // نام schema فقط از GUID تولیدشده در همین آزمون است؛ DDL شناسه پارامترپذیر ندارد.
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(_connectionString) { SearchPath = _schema };
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString,
                options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).Options;
        await using var db = new ApplicationDbContext(_options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // حذف فقط schema اختصاصی و تصادفی همین آزمون را هدف می‌گیرد.
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    [CartPostgresFact]
    public async Task ReaderEnforcesOwnershipFiltersPagingAndHasNoWrites()
    {
        var buyer = User.CreateInitial("history-buyer");
        var otherBuyer = User.CreateInitial("history-other");
        var time = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, ["free"], "FREE");
        cart.SetQuote([new CourseCartQuoteItem("free", "دوره رایگان", 100, 0, 100, 0)]);
        cart.MarkPaid();
        var free = CreatePurchase(buyer, "free", 0, time, null);
        free.AttachToCart(cart.Id);
        var paid = CreatePurchase(buyer, "paid", 51.125m, time.AddMinutes(1), 12000);
        paid.MarkNotificationSent();
        var retry = CreatePurchase(buyer, "retry", 20, time.AddMinutes(2), 15000);
        retry.RecordNotificationFailure("تحویل ناموفق است.");
        var foreign = CreatePurchase(otherBuyer, "other", 70, time.AddMinutes(3), 13000);
        var pending = CoursePurchase.Create(buyer.Id, ProductSnapshot.Create("pending", "در انتظار پرداخت", 10));

        await using (var seed = new ApplicationDbContext(_options))
        {
            seed.AddRange(buyer, otherBuyer, cart, free, paid, retry, foreign, pending);
            await seed.SaveChangesAsync();
        }
        await using var db = new ApplicationDbContext(_options);
        var reader = new PurchaseHistoryReader(db);
        var purchasesBefore = await db.Set<CoursePurchase>().CountAsync();
        var ledgerBefore = await db.Set<LedgerEntry>().CountAsync();
        var walletsBefore = await db.Set<Wallet>().CountAsync();

        var first = await reader.ListAsync(buyer.Id.Value, new PurchaseHistoryFilter(PageSize: 2), default);
        first.TotalCount.Should().Be(3);
        first.Items.Select(x => x.PurchaseId).Should().Equal(retry.Id.Value, paid.Id.Value);
        var second = await reader.ListAsync(buyer.Id.Value, new PurchaseHistoryFilter(2, 2), default);
        second.Items.Single().PurchaseId.Should().Be(free.Id.Value);
        (await reader.ListAsync(buyer.Id.Value, new PurchaseHistoryFilter(9, 2), default)).Items.Should().BeEmpty();

        (await reader.GetReceiptAsync(foreign.Id.Value, buyer.Id.Value, default)).Should().BeNull();
        (await reader.GetReceiptAsync(Guid.NewGuid(), buyer.Id.Value, default)).Should().BeNull();
        (await reader.GetReceiptAsync(pending.Id.Value, buyer.Id.Value, default)).Should().BeNull();
        var adminReceipt = await reader.GetAdminReceiptAsync(foreign.Id.Value, default);
        adminReceipt!.Purchase!.Id.Should().Be(foreign.Id);
        (await reader.GetAdminReceiptAsync(Guid.NewGuid(), default)).Should().BeNull();
        (await reader.GetAdminReceiptAsync(pending.Id.Value, default)).Should().BeNull();
        var freeData = await reader.GetReceiptAsync(free.Id.Value, buyer.Id.Value, default);
        var adminFreeData = await reader.GetAdminReceiptAsync(free.Id.Value, default);
        adminFreeData!.Cart!.Id.Should().Be(cart.Id);
        var receipt = PurchaseHistoryMapping.ToReceipt(freeData!);
        receipt.FinancialStatus.Should().Be("Paid");
        receipt.DeliveryStatus.Should().Be("Pending");
        receipt.FinalAmountInNoor.Should().Be(0);
        receipt.DiscountAmountInNoor.Should().Be(100);
        receipt.ReferenceRialPerNoorAtPurchaseTime.Should().BeNull();
        var paidReceipt = PurchaseHistoryMapping.ToReceipt(
            (await reader.GetReceiptAsync(paid.Id.Value, buyer.Id.Value, default))!);
        paidReceipt.ReferenceRialPerNoorAtPurchaseTime.Should().Be(12000);
        paidReceipt.FinalAmountInNoor.Should().Be(51.125m);

        var filter = new PurchaseHistoryFilter(BuyerExternalUserId: buyer.ExternalUserId,
            FromUtc: time.AddMinutes(1), ToUtc: time.AddMinutes(2),
            FinancialStatus: "Paid", DeliveryStatus: "Delivered");
        var admin = await reader.ListAsync(null, filter, default);
        admin.Items.Single().PurchaseId.Should().Be(paid.Id.Value);
        var waiting = await reader.ListAsync(null,
            new PurchaseHistoryFilter(BuyerExternalUserId: buyer.ExternalUserId, DeliveryStatus: "Pending"), default);
        waiting.TotalCount.Should().Be(2);
        var all = await reader.ListAsync(null, new PurchaseHistoryFilter(), default);
        all.TotalCount.Should().Be(4);

        await PurchaseHistoryHttpChecks.VerifyAsync(_options, buyer.Id.Value, free.Id.Value, foreign.Id.Value);

        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Set<CoursePurchase>().CountAsync()).Should().Be(purchasesBefore);
        (await db.Set<LedgerEntry>().CountAsync()).Should().Be(ledgerBefore);
        (await db.Set<Wallet>().CountAsync()).Should().Be(walletsBefore);
        var states = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
        states.Single(x => x.Id == free.Id).Status.Should().Be(CoursePurchaseStatus.Debited);
        states.Single(x => x.Id == retry.Id).NotificationAttempts.Should().Be(1);
        states.Single(x => x.Id == pending.Id).Status.Should().Be(CoursePurchaseStatus.Pending);

    }

    private static CoursePurchase CreatePurchase(
        User buyer, string courseId, decimal amount, DateTime time, decimal? rate)
    {
        var snapshot = ProductSnapshot.Create(courseId, "عنوان تاریخی دوره", amount,
            isFree: amount == 0, referenceRialPerNoorAtPurchaseTime: rate);
        typeof(ProductSnapshot).GetProperty(nameof(ProductSnapshot.PurchasedAt))!.SetValue(snapshot, time);
        var purchase = CoursePurchase.Create(buyer.Id, snapshot, isFree: amount == 0);
        purchase.MarkDebited(Guid.NewGuid());
        return purchase;
    }

    [CartPostgresFact]
    public async Task OrderReaderKeepsConsistentSnapshotAcrossConcurrentCommit()
    {
        var buyer = User.CreateInitial("tracking-concurrent");
        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, ["concurrent"], null);
        cart.SetQuote([new CourseCartQuoteItem("concurrent", "دوره آزمون", 20, 0, 0, 20)]);
        await using (var seed = new ApplicationDbContext(_options))
        {
            seed.AddRange(buyer, cart);
            await seed.SaveChangesAsync();
        }

        var interceptor = new CommitAfterCartRead(async () =>
        {
            await using var writer = new ApplicationDbContext(_options);
            var storedCart = await writer.Set<CourseCart>().SingleAsync(item => item.Id == cart.Id);
            storedCart.MarkPaid();
            var purchase = CreatePurchase(buyer, "concurrent", 20, DateTime.UtcNow, null);
            purchase.AttachToCart(cart.Id);
            writer.Add(purchase);
            await writer.SaveChangesAsync();
        });
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(_options).AddInterceptors(interceptor).Options;
        await using var db = new ApplicationDbContext(options);
        var reader = new CourseOrderReader(db);
        var before = CourseOrderMapping.Build(await reader.ReadAsync(buyer.Id.Value, null, default)).Single().Details;
        interceptor.Committed.Should().BeTrue();
        before.FinancialStatus.Should().Be("AwaitingPayment");
        before.Items.Single().PurchaseId.Should().BeNull();
        var after = CourseOrderMapping.Build(await reader.ReadAsync(buyer.Id.Value, null, default)).Single().Details;
        after.FinancialStatus.Should().Be("Paid");
        after.Items.Single().PurchaseId.Should().NotBeNull();
        before.OrderId.Should().Be(after.OrderId);
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Set<LedgerEntry>().CountAsync()).Should().Be(0);
        (await db.Set<Wallet>().CountAsync()).Should().Be(0);
    }

    [CartPostgresFact]
    public async Task OrderReaderPreservesHistoricalTopUpsAndLegacyLinkWithoutDuplicatesOrWrites()
    {
        var buyer = User.CreateInitial("tracking-history");
        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, ["cart-course"], null);
        cart.SetQuote([new CourseCartQuoteItem("cart-course", "دوره سبد", 40, 0, 0, 40)]);
        var first = TopUpRequest.CreateGateway(buyer.Id, 40, 1000);
        var second = TopUpRequest.CreateGateway(buyer.Id, 40, 1000);
        cart.AwaitTopUp(first.Id.Value);
        first.Reject("رد آزمون");
        cart.Fail();
        cart.RecordSynchronization(true, null);
        cart.Reopen();
        cart.SetQuote([new CourseCartQuoteItem("cart-course", "دوره سبد", 40, 0, 0, 40)]);
        cart.AwaitTopUp(second.Id.Value);
        second.Confirm("confirmed-cart");
        cart.Fail();
        var legacy = TopUpRequest.CreateGateway(buyer.Id, 20, 1000,
            [new PendingItemSnapshot(PendingItemType.Course, "legacy-course", 20)]);
        legacy.Confirm("confirmed-legacy");
        var purchase = CreatePurchase(buyer, "legacy-course", 20, DateTime.UtcNow, null);
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{legacy.Id.Value:N}:legacy-course"));
        typeof(CoursePurchase).GetProperty(nameof(CoursePurchase.Id))!.SetValue(
            purchase, new CoursePurchaseId(new Guid(hash.AsSpan(0, 16))));
        await using (var seed = new ApplicationDbContext(_options))
        {
            seed.AddRange(buyer, cart, first, second, legacy, purchase);
            await seed.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(_options);
        var reader = new CourseOrderReader(db);
        var rows = CourseOrderMapping.Build(await reader.ReadAsync(null, buyer.ExternalUserId, default));
        rows.Should().HaveCount(2);
        var cartOrder = rows.Single(row => row.Details.SourceType == "Cart").Details;
        cartOrder.FinancialStatus.Should().Be("Failed");
        cartOrder.TopUps.Should().HaveCount(2);
        cartOrder.TopUps.Select(topUp => topUp.TopUpRequestId).Should().Contain([first.Id.Value, second.Id.Value]);
        var legacyOrder = rows.Single(row => row.Details.SourceType == "TopUpCheckout").Details;
        legacyOrder.FinancialStatus.Should().Be("Paid");
        legacyOrder.Items.Single().PurchaseId.Should().Be(purchase.Id.Value);
        (await reader.ReadAsync(Guid.NewGuid(), null, default)).Carts.Should().BeEmpty();
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Set<CoursePurchase>().CountAsync()).Should().Be(1);
        (await db.Set<TopUpRequest>().CountAsync()).Should().Be(3);
        (await db.Set<LedgerEntry>().CountAsync()).Should().Be(0);
        (await db.Set<Wallet>().CountAsync()).Should().Be(0);
    }

    private sealed class CommitAfterCartRead(Func<Task> commit) : DbCommandInterceptor
    {
        public bool Committed { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!Committed && command.CommandText.Contains("CourseCarts", StringComparison.Ordinal))
            {
                Committed = true;
                await commit();
            }
            return result;
        }
    }
}
