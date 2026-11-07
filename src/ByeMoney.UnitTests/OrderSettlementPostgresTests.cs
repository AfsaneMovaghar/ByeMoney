using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriodOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriods;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;
using ByeMoney.Application.Modules.Purchases.Settlements.Constants;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByeMoney.UnitTests;

/// <summary>
/// پذیرش PostgreSQL تسویهٔ سفارش‌های بای‌مانی: یکتایی سفارش در دوره‌ها، بستن هم‌زمان و نبود اثر مالی.
/// </summary>
public sealed class OrderSettlementPostgresTests : IAsyncLifetime
{
    private readonly string _schema = "settlement_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;
    private IConfiguration _configuration = null!;

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

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(directory!.FullName, "src", "ByeMoney.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        _connectionString = _configuration.GetConnectionString("DefaultConnection")!;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // نام schema فقط از GUID تولیدشده در همین آزمون است؛ DDL شناسه پارامترپذیر ندارد.
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(_connectionString) { SearchPath = _schema, Pooling = false };
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString,
                options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
            .Options;
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
    public async Task SequentialPeriodsNeverRepeatOrdersAndUnshippedOrderJoinsNextPeriod()
    {
        var seed = await SeedAsync();
        await using var provider = BuildProvider();
        var ledgerCount = await LedgerCountAsync();

        var firstPreview = await SendAsync(provider, seed.AdminA, new GetOrderSettlementPreviewQuery());
        firstPreview.OrderCount.Should().Be(1);
        firstPreview.TotalNetAmountInNoor.Should().Be(40m);
        firstPreview.Orders.Items.Single().OrderId.Should().Be(seed.CourseOrderId);

        var first = await CloseAsync(provider, seed.AdminA, firstPreview, "آبان");
        first.IsSuccess.Should().BeTrue();
        first.Value.OrderCount.Should().Be(1);
        first.Value.TotalNetAmountInNoor.Should().Be(40m);
        first.Value.Title.Should().Be("آبان");
        first.Value.ClosedByExternalUserId.Should().Be(seed.AdminAExternalId);

        var emptyPreview = await SendAsync(provider, seed.AdminA, new GetOrderSettlementPreviewQuery());
        emptyPreview.OrderCount.Should().Be(0);
        var empty = await CloseAsync(provider, seed.AdminA, emptyPreview, null);
        empty.ErrorCode.Should().Be(OrderSettlementErrorCodes.Empty);

        await ShipAsync(seed.PhysicalPurchaseId);
        var secondPreview = await SendAsync(provider, seed.AdminA, new GetOrderSettlementPreviewQuery());
        secondPreview.Orders.Items.Select(order => order.OrderId).Should().Equal(seed.PhysicalOrderId);
        secondPreview.TotalNetAmountInNoor.Should().Be(110m);

        var second = await CloseAsync(provider, seed.AdminB, secondPreview, null);
        second.IsSuccess.Should().BeTrue();

        var periods = await SendAsync(provider, seed.AdminA, new GetOrderSettlementPeriodsQuery());
        periods.TotalCount.Should().Be(2);
        periods.Items.Select(period => period.PeriodId).Should().Equal(second.Value.PeriodId, first.Value.PeriodId);

        var firstOrders = await SendAsync(provider, seed.AdminA,
            new GetOrderSettlementPeriodOrdersQuery(first.Value.PeriodId));
        firstOrders.Items.Single().Should().BeEquivalentTo(
            new { OrderId = seed.CourseOrderId, NetAmountInNoor = 40m, BuyerExternalUserId = seed.BuyerExternalId });

        var missing = () => SendAsync(provider, seed.AdminA, new GetOrderSettlementPeriodOrdersQuery(Guid.NewGuid()));
        await missing.Should().ThrowAsync<NotFoundException>();

        await using var db = new ApplicationDbContext(_options);
        (await db.Set<OrderSettlementItem>().CountAsync()).Should().Be(2);
        (await LedgerCountAsync()).Should().Be(ledgerCount);
    }

    [CartPostgresFact]
    public async Task ConcurrentClosesProduceExactlyOneSuccess()
    {
        var seed = await SeedAsync();
        await ShipAsync(seed.PhysicalPurchaseId);
        var barrier = new InsertBarrier(participants: 2);
        await using var provider = BuildProvider(barrier);
        var ledgerCount = await LedgerCountAsync();
        var preview = await SendAsync(provider, seed.AdminA, new GetOrderSettlementPreviewQuery());
        preview.OrderCount.Should().Be(2);

        // هر دو بستن فهرست را پیش از commit دیگری محاسبه می‌کنند و فقط قید دیتابیس می‌تواند یکی را رد کند.
        var results = await Task.WhenAll(
            Task.Run(() => CloseAsync(provider, seed.AdminA, preview, "A")),
            Task.Run(() => CloseAsync(provider, seed.AdminB, preview, "B")));

        results.Count(result => result.IsSuccess).Should().Be(1);
        var failed = results.Single(result => result.IsFailure);
        failed.Status.Should().Be(ResultStatus.Conflict);
        failed.ErrorCode.Should().Be(OrderSettlementErrorCodes.AlreadySettled);

        await using var db = new ApplicationDbContext(_options);
        (await db.Set<OrderSettlementPeriod>().CountAsync()).Should().Be(1);
        (await db.Set<OrderSettlementItem>().Select(item => item.OrderId).ToListAsync())
            .Should().BeEquivalentTo([seed.CourseOrderId, seed.PhysicalOrderId]);
        (await LedgerCountAsync()).Should().Be(ledgerCount);
    }

    [CartPostgresFact]
    public async Task DatabaseRejectsSameOrderInSecondPeriod()
    {
        var seed = await SeedAsync();
        var orderId = seed.CourseOrderId;

        await using (var db = new ApplicationDbContext(_options))
        {
            var store = new OrderSettlementStore(db);
            (await store.TryAddAsync(Period(seed.AdminA, orderId), default)).Should().BeTrue();
        }

        await using (var db = new ApplicationDbContext(_options))
        {
            var store = new OrderSettlementStore(db);
            (await store.TryAddAsync(Period(seed.AdminB, orderId), default)).Should().BeFalse();
        }

        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<OrderSettlementPeriod>().CountAsync()).Should().Be(1);
        (await verify.Set<OrderSettlementItem>().CountAsync()).Should().Be(1);
    }

    private static OrderSettlementPeriod Period(Guid adminId, string orderId) =>
        OrderSettlementPeriod.Close(new UserId(adminId), null, null,
            [OrderSettlementItem.Create(orderId, "Purchase", "buyer", DateTime.UtcNow, 40m)]);

    private ServiceProvider BuildProvider(InsertBarrier? barrier = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(_configuration);
        services.AddSingleton(_options);
        services.AddScoped<ApplicationDbContext>();
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<TestCurrentUser>());

        if (barrier is not null)
        {
            services.AddSingleton(barrier);
            services.AddScoped<OrderSettlementStore>();
            services.AddScoped<IOrderSettlementStore>(sp => new BarrierOrderSettlementStore(
                sp.GetRequiredService<OrderSettlementStore>(), sp.GetRequiredService<InsertBarrier>()));
        }

        return services.BuildServiceProvider();
    }

    private static async Task<T> SendAsync<T>(ServiceProvider provider, Guid adminId, IRequest<T> request)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserId = adminId;
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static Task<Result<OrderSettlementPeriodResponse>> CloseAsync(
        ServiceProvider provider, Guid adminId, OrderSettlementPreviewResponse preview, string? title) =>
        SendAsync(provider, adminId, new CloseOrderSettlementPeriodCommand(title, null, preview.PreviewToken));

    private async Task<int> LedgerCountAsync()
    {
        await using var db = new ApplicationDbContext(_options);
        return await db.Set<LedgerEntry>().CountAsync();
    }

    private async Task ShipAsync(Guid purchaseId)
    {
        await using var db = new ApplicationDbContext(_options);
        var purchase = await db.Set<PhysicalProductPurchase>()
            .SingleAsync(item => item.Id == new PhysicalProductPurchaseId(purchaseId));
        purchase.MarkShipped();
        await db.SaveChangesAsync();
    }

    private async Task<SeedData> SeedAsync()
    {
        var buyer = User.CreateInitial("settlement-buyer-" + Guid.NewGuid().ToString("N"));
        var adminA = User.CreateInitial("settlement-admin-a-" + Guid.NewGuid().ToString("N"));
        var adminB = User.CreateInitial("settlement-admin-b-" + Guid.NewGuid().ToString("N"));

        var course = CoursePurchase.Create(buyer.Id, ProductSnapshot.Create("settled-course", "Course", 40m));
        course.MarkDebited(Guid.NewGuid());

        var cart = Cart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, [
            new CartItemInput("book", ProductItemType.Product, 2)
        ], null);
        cart.SetQuote([
            new CartQuoteItem("book", "Book", 50, 0, 0, 100, ProductItemType.Product, null, 2)
        ], shippingAmountInNoor: 10);
        cart.MarkPaid();
        var book = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), cart.Id, buyer.Id, "book", "Book", 2, 50, 100);
        book.MarkDebited(cart.Id);

        await using var db = new ApplicationDbContext(_options);
        db.AddRange(buyer, adminA, adminB, course, cart, book);
        await db.SaveChangesAsync();

        return new SeedData(
            buyer.ExternalUserId,
            adminA.Id.Value,
            adminA.ExternalUserId,
            adminB.Id.Value,
            "purchase:" + course.Id.Value.ToString("D"),
            "cart:" + cart.Id.ToString("D"),
            book.Id.Value);
    }

    private sealed record SeedData(
        string BuyerExternalId,
        Guid AdminA,
        string AdminAExternalId,
        Guid AdminB,
        string CourseOrderId,
        string PhysicalOrderId,
        Guid PhysicalPurchaseId);

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; }
    }

    /// <summary>هر شرکت‌کننده پیش از درج منتظر می‌ماند تا همه به همین نقطه برسند.</summary>
    private sealed class InsertBarrier(int participants)
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) == participants)
            {
                _allArrived.SetResult();
            }

            return _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class BarrierOrderSettlementStore(IOrderSettlementStore inner, InsertBarrier barrier) :
        IOrderSettlementStore
    {
        public Task<IReadOnlySet<string>> GetSettledOrderIdsAsync(CancellationToken ct) =>
            inner.GetSettledOrderIdsAsync(ct);

        public async Task<bool> TryAddAsync(OrderSettlementPeriod period, CancellationToken ct)
        {
            await barrier.ArriveAsync();
            return await inner.TryAddAsync(period, ct);
        }

        public Task<OrderSettlementPeriodResponse?> FindPeriodAsync(Guid periodId, CancellationToken ct) =>
            inner.FindPeriodAsync(periodId, ct);

        public Task<PurchasePage<OrderSettlementPeriodResponse>> GetPeriodsAsync(
            int page, int pageSize, CancellationToken ct) =>
            inner.GetPeriodsAsync(page, pageSize, ct);

        public Task<bool> PeriodExistsAsync(Guid periodId, CancellationToken ct) =>
            inner.PeriodExistsAsync(periodId, ct);

        public Task<PurchasePage<SettlementOrderResponse>> GetPeriodOrdersAsync(
            Guid periodId, int page, int pageSize, CancellationToken ct) =>
            inner.GetPeriodOrdersAsync(periodId, page, pageSize, ct);
    }
}
