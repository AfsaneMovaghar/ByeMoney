using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ByeMoney.API;
using ByeMoney.API.Controllers;
using ByeMoney.API.Middleware;
using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ByeMoney.Domain.Modules.Identity.Permissions;
using ByeMoney.Domain.Modules.Identity.Roles;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Modules.Identity.Persistence.Permission;
using ByeMoney.Infrastructure.Modules.Identity.Services;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ByeMoney.UnitTests;

/// <summary>
/// پذیرش HTTP قرارداد I11-O با JWT امضاشدهٔ واقعی، RBAC واقعی از دیتابیس و PostgreSQL.
/// </summary>
public sealed class PhysicalDeliveryHttpPostgresTests : IAsyncLifetime
{
    private const string JwtSecret = "physical-delivery-http-tests-secret-at-least-32-bytes";
    private const string CancellationReason = "انصراف خریدار پیش از ارسال";

    private static readonly string[] ReceiptKeys =
    [
        "orderId", "sourceType", "cartId", "transactionId", "purchasedAtUtc", "couponCode",
        "finalAmountInNoor", "financialStatus", "deliveryStatus", "cartSummary", "items"
    ];

    private static readonly string[] ReceiptItemKeys =
    [
        "purchaseId", "externalId", "title", "itemType", "parentExternalId", "quantity", "unitPriceInNoor",
        "baseAmountInNoor", "discountAmountInNoor", "finalAmountInNoor", "referenceRialPerNoorAtPurchaseTime",
        "deliveryStatus", "physicalDeliveryStatus", "purchasedAtUtc", "deliveredAtUtc", "cancelledQuantity",
        "refundedRegularInNoor", "refundedGiftInNoor"
    ];

    private static readonly string[] DeliveryResponseKeys =
        ["purchaseId", "deliveryStatus", "shippedAtUtc", "deliveredAtUtc"];

    private readonly string _schema = "physical_http_test_" + Guid.NewGuid().ToString("N");
    private readonly Mock<ICartPricingClient> _pricing = new();
    private readonly Mock<IPhysicalStockClient> _stock = new();
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
        var builder = new NpgsqlConnectionStringBuilder(_connectionString) { SearchPath = _schema, Pooling = false };
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString,
                options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).Options;
        await using var db = new ApplicationDbContext(_options);
        await db.Database.MigrateAsync();

        _pricing
            .Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _stock
            .Setup(x => x.RestockAsync(It.IsAny<PhysicalRestockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
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
    public async Task ReceiptAndDeliveryContractHoldsOverHttpWithRealJwt()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var orderId = $"cart:{seed.CartId:D}";

        await VerifyReceiptAccessAsync(client, seed, orderId);
        await VerifyReceiptShapeAsync(client, seed, orderId);
        await VerifyDeliveryAuthorizationAsync(client, seed);
        await VerifyDeliveryTransitionsAsync(client, seed, orderId);
        await VerifyMoneyUntouchedAsync(seed);
    }

    [CartPostgresFact]
    public async Task PhysicalCancellationContractHoldsOverHttpWithRealJwt()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var seed = await SeedCancellationAsync(app.Services);

        await VerifyBuyerCancellationAsync(client, seed);
        await VerifyAdminCancellationAsync(client, seed);
    }

    private async Task VerifyBuyerCancellationAsync(HttpClient client, CancellationSeed seed)
    {
        var url = $"/api/me/physical-orders/{seed.BuyerCartId}/cancel";
        var operationId = Guid.NewGuid();
        var body = new { operationId, reason = CancellationReason };

        (await SendAsync(client, HttpMethod.Post, url, null, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(client, HttpMethod.Post, url, Token(seed.OtherUser), body)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await CancellationStateAsync(seed)).Should().Be(seed.StartState);

        var owner = await SendAsync(client, HttpMethod.Post, url, Token(seed.Buyer), body);
        owner.StatusCode.Should().Be(HttpStatusCode.OK);
        using var ownerBody = JsonDocument.Parse(await owner.Content.ReadAsStringAsync());
        ownerBody.RootElement.GetProperty("outcome").GetString().Should().Be("Refunded");
        ownerBody.RootElement.GetProperty("totalRefundInNoor").GetDecimal().Should().Be(100m);
        var afterFirst = await CancellationStateAsync(seed);
        afterFirst.Should().Be(seed.StartState with
        {
            Balance = seed.StartState.Balance + 100m,
            LedgerCount = afterFirst.LedgerCount,
            CancellationRows = 1
        });
        afterFirst.LedgerCount.Should().BeGreaterThan(seed.StartState.LedgerCount);

        var replay = await SendAsync(client, HttpMethod.Post, url, Token(seed.Buyer), body);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        replayBody.RootElement.GetProperty("totalRefundInNoor").GetDecimal().Should().Be(100m);
        (await CancellationStateAsync(seed)).Should().Be(afterFirst);
    }

    private async Task VerifyAdminCancellationAsync(HttpClient client, CancellationSeed seed)
    {
        var url = $"/api/admin/physical-purchases/{seed.AdminPurchaseId}/cancel";
        var operationId = Guid.NewGuid();
        var body = new { operationId, quantity = 1, reason = CancellationReason };
        var before = await CancellationStateAsync(seed);

        (await SendAsync(client, HttpMethod.Post, url, null, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(client, HttpMethod.Post, url, Token(seed.Buyer), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Post, url, Token(seed.CoursesManageAdmin), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await CancellationStateAsync(seed)).Should().Be(before);

        var allowed = await SendAsync(client, HttpMethod.Post, url, Token(seed.NoorInjectAdmin), body);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var allowedBody = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        allowedBody.RootElement.GetProperty("actor").GetString().Should().Be("Admin");
        allowedBody.RootElement.GetProperty("totalRefundInNoor").GetDecimal().Should().Be(30m);
        var afterFirst = await CancellationStateAsync(seed);
        afterFirst.Balance.Should().Be(before.Balance + 30m);
        afterFirst.CancellationRows.Should().Be(before.CancellationRows + 1);

        var replay = await SendAsync(client, HttpMethod.Post, url, Token(seed.NoorInjectAdmin), body);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CancellationStateAsync(seed)).Should().Be(afterFirst);
    }

    private async Task<CancellationState> CancellationStateAsync(CancellationSeed seed)
    {
        await using var db = new ApplicationDbContext(_options);
        var wallet = await db.Set<Wallet>().AsNoTracking().SingleAsync(item => item.UserId == new UserId(seed.BuyerId));
        return new CancellationState(
            wallet.WithdrawableBalance,
            await db.Set<LedgerEntry>().CountAsync(),
            await db.Set<PhysicalProductCancellation>().CountAsync());
    }

    private async Task<CancellationSeed> SeedCancellationAsync(IServiceProvider services)
    {
        var buyer = User.CreateInitial("http-cancel-buyer");
        var other = User.CreateInitial("http-cancel-other");
        var manageAdmin = User.CreateInitial("http-cancel-manage-admin");
        var noorAdmin = User.CreateInitial("http-cancel-noor-admin");
        var manageRole = Role.Create("CancelCoursesManageOnly");
        var noorRole = Role.Create("CancelNoorInjectOnly");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.AddRange(buyer, other, manageAdmin, noorAdmin, manageRole, noorRole);
        await db.SaveChangesAsync();
        db.AddRange(
            RolePermission.Create(manageRole.Id, PermissionConfiguration.CoursesManagePermissionId),
            RolePermission.Create(noorRole.Id, PermissionConfiguration.NoorInjectPermissionId),
            UserRole.Create(manageAdmin.Id, manageRole.Id),
            UserRole.Create(noorAdmin.Id, noorRole.Id));
        await db.SaveChangesAsync();

        var provisioning = scope.ServiceProvider.GetRequiredService<IUserWalletProvisioningService>();
        var wallet = await provisioning.GetOrCreateUserWalletAsync(buyer.Id);
        wallet.Wallet.ApplyCredit(1000m);
        await provisioning.GetOrCreateSystemAccountAsync();
        await db.SaveChangesAsync();

        var carts = scope.ServiceProvider.GetRequiredService<ICartStore>();
        var buyerCart = await PayCartAsync(carts, buyer, "cancel-book", unitPrice: 50m, quantity: 2);
        var adminCart = await PayCartAsync(carts, buyer, "cancel-pen", unitPrice: 30m, quantity: 2);
        var adminPurchaseId = await db.Set<PhysicalProductPurchase>()
            .Where(item => item.CartId == adminCart.Id)
            .Select(item => item.Id.Value)
            .SingleAsync();

        var seed = new CancellationSeed(
            buyer.ExternalUserId, other.ExternalUserId, manageAdmin.ExternalUserId, noorAdmin.ExternalUserId,
            buyer.Id.Value, buyerCart.Id, adminPurchaseId, default!);
        return seed with { StartState = await CancellationStateAsync(seed) };
    }

    private static async Task<Cart> PayCartAsync(
        ICartStore carts, User buyer, string externalId, decimal unitPrice, int quantity)
    {
        var cart = Cart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, [
            new CartItemInput(externalId, ProductItemType.Product, quantity)
        ], null);
        (await carts.CreateAsync(cart, default)).IsSuccess.Should().BeTrue();
        var quote = new CartQuoteItem(
            externalId, externalId, unitPrice, 0, 0, unitPrice * quantity, ProductItemType.Product, null, quantity);
        await carts.SetQuoteAsync(cart.Id, buyer.Id.Value, [quote], 0m, null, cart.ReservationId, default);
        (await carts.PayAsync(cart.Id, buyer.Id.Value, cart.ReservationId, default)).IsSuccess.Should().BeTrue();
        return cart;
    }

    private static async Task VerifyReceiptAccessAsync(HttpClient client, SeedData seed, string orderId)
    {
        var adminUrl = $"/api/admin/course-orders/{orderId}/receipt";
        var meUrl = $"/api/me/course-orders/{orderId}/receipt";

        (await SendAsync(client, HttpMethod.Get, adminUrl, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(client, HttpMethod.Get, adminUrl, Token(seed.PlainUser))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Get, adminUrl, Token(seed.Buyer))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Get, adminUrl, Token(seed.CoursesManageAdmin))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Get, adminUrl, Token(seed.PurchasesViewAdmin))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await SendAsync(client, HttpMethod.Get, meUrl, Token(seed.Buyer))).StatusCode.Should().Be(HttpStatusCode.OK);
        var foreign = await SendAsync(client, HttpMethod.Get, meUrl, Token(seed.PlainUser));
        var missing = await SendAsync(
            client, HttpMethod.Get, $"/api/me/course-orders/cart:{Guid.NewGuid():D}/receipt", Token(seed.PlainUser));
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreign.Content.ReadAsStringAsync()).Should().Be(await missing.Content.ReadAsStringAsync());

        var unpaidUrl = $"/api/admin/course-orders/cart:{seed.UnpaidCartId:D}/receipt";
        (await SendAsync(client, HttpMethod.Get, unpaidUrl, Token(seed.PurchasesViewAdmin))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task VerifyReceiptShapeAsync(HttpClient client, SeedData seed, string orderId)
    {
        var response = await SendAsync(
            client, HttpMethod.Get, $"/api/admin/course-orders/{orderId}/receipt", Token(seed.PurchasesViewAdmin));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        PropertyNames(root).Should().BeEquivalentTo(ReceiptKeys);
        root.GetProperty("orderId").GetString().Should().Be(orderId);
        root.GetProperty("sourceType").GetString().Should().Be("Cart");
        root.GetProperty("financialStatus").GetString().Should().Be("Paid");
        root.GetProperty("finalAmountInNoor").GetDecimal().Should().Be(130m);
        root.GetProperty("cartSummary").GetProperty("shippingAmountInNoor").GetDecimal().Should().Be(10m);
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(2);
        foreach (var item in items)
        {
            PropertyNames(item).Should().BeEquivalentTo(ReceiptItemKeys);
            item.GetProperty("itemType").GetString().Should().Be("Product");
        }

        items.Sum(item => item.GetProperty("finalAmountInNoor").GetDecimal()).Should().Be(120m);
        var book = items.Single(item => item.GetProperty("externalId").GetString() == "book");
        book.GetProperty("quantity").GetInt32().Should().Be(2);
        book.GetProperty("physicalDeliveryStatus").GetString().Should().Be("Preparing");
        book.GetProperty("deliveryStatus").GetString().Should().Be("Pending");
        book.GetProperty("refundedRegularInNoor").GetDecimal().Should().Be(0m);
        book.GetProperty("refundedGiftInNoor").GetDecimal().Should().Be(0m);
    }

    private static async Task VerifyDeliveryAuthorizationAsync(HttpClient client, SeedData seed)
    {
        var url = $"/api/admin/physical-purchases/{seed.BookId}/delivery-status";
        var body = new { status = "Shipped" };

        (await SendAsync(client, HttpMethod.Put, url, null, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(client, HttpMethod.Put, url, Token(seed.PlainUser), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Put, url, Token(seed.Buyer), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Put, url, Token(seed.PurchasesViewAdmin), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task VerifyDeliveryTransitionsAsync(HttpClient client, SeedData seed, string orderId)
    {
        var admin = Token(seed.CoursesManageAdmin);
        var url = $"/api/admin/physical-purchases/{seed.BookId}/delivery-status";

        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Delivered" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Preparing" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Bogus" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(
            client, HttpMethod.Put, $"/api/admin/physical-purchases/{Guid.NewGuid()}/delivery-status",
            admin, new { status = "Shipped" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var shipped = await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Shipped" });
        shipped.StatusCode.Should().Be(HttpStatusCode.OK);
        using var shippedBody = JsonDocument.Parse(await shipped.Content.ReadAsStringAsync());
        PropertyNames(shippedBody.RootElement).Should().BeEquivalentTo(DeliveryResponseKeys);
        shippedBody.RootElement.GetProperty("purchaseId").GetGuid().Should().Be(seed.BookId);
        shippedBody.RootElement.GetProperty("deliveryStatus").GetString().Should().Be("Shipped");
        shippedBody.RootElement.GetProperty("shippedAtUtc").ValueKind.Should().Be(JsonValueKind.String);
        shippedBody.RootElement.GetProperty("deliveredAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        var shippedAt = shippedBody.RootElement.GetProperty("shippedAtUtc").GetDateTime();

        await AssertOrderItemStatusAsync(client, seed, orderId, "Shipped");

        var repeated = await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Shipped" });
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        using var repeatedBody = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
        repeatedBody.RootElement.GetProperty("shippedAtUtc").GetDateTime().Should().BeCloseTo(shippedAt, TimeSpan.FromMilliseconds(1));

        var delivered = await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Delivered" });
        delivered.StatusCode.Should().Be(HttpStatusCode.OK);
        using var deliveredBody = JsonDocument.Parse(await delivered.Content.ReadAsStringAsync());
        deliveredBody.RootElement.GetProperty("deliveryStatus").GetString().Should().Be("Delivered");
        deliveredBody.RootElement.GetProperty("shippedAtUtc").GetDateTime().Should().BeCloseTo(shippedAt, TimeSpan.FromMilliseconds(1));
        deliveredBody.RootElement.GetProperty("deliveredAtUtc").ValueKind.Should().Be(JsonValueKind.String);

        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Shipped" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(client, HttpMethod.Put, url, admin, new { status = "Delivered" })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        await AssertOrderItemStatusAsync(client, seed, orderId, "Delivered");
    }

    private static async Task AssertOrderItemStatusAsync(
        HttpClient client, SeedData seed, string orderId, string expected)
    {
        var mine = await SendAsync(client, HttpMethod.Get, "/api/me/course-orders", Token(seed.Buyer));
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        using var page = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        page.RootElement.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("orderId").GetString() == orderId);

        var details = await SendAsync(client, HttpMethod.Get, $"/api/me/course-orders/{orderId}", Token(seed.Buyer));
        using var detailsBody = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        detailsBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("externalCourseId").GetString() == "book")
            .GetProperty("physicalDeliveryStatus").GetString().Should().Be(expected);

        var receipt = await SendAsync(
            client, HttpMethod.Get, $"/api/me/course-orders/{orderId}/receipt", Token(seed.Buyer));
        using var receiptBody = JsonDocument.Parse(await receipt.Content.ReadAsStringAsync());
        receiptBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("externalId").GetString() == "book")
            .GetProperty("physicalDeliveryStatus").GetString().Should().Be(expected);
    }

    private async Task VerifyMoneyUntouchedAsync(SeedData seed)
    {
        await using var db = new ApplicationDbContext(_options);
        var wallet = await db.Set<Wallet>().AsNoTracking().SingleAsync(item => item.UserId == new UserId(seed.BuyerId));
        wallet.WithdrawableBalance.Should().Be(seed.WalletBalance);
        (await db.Set<LedgerEntry>().CountAsync()).Should().Be(seed.LedgerCount);
        var book = await db.Set<PhysicalProductPurchase>().AsNoTracking().SingleAsync(item => item.Id == new PhysicalProductPurchaseId(seed.BookId));
        book.PaidAmountInNoor.Should().Be(100m);
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(settings => settings.ValidateOnBuild = false);
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Strapi:JwtSecret"] = JwtSecret });
        builder.Services.AddPresentationServices(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminPhysicalPurchasesController).Assembly);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        // کارهای پس‌زمینهٔ واقعی نباید در آزمون اجرا شوند و دیتابیس باید همان schema اختصاصی آزمون باشد.
        RemoveApplicationHostedServices(builder.Services);
        builder.Services.AddSingleton(_options);
        builder.Services.AddScoped<ApplicationDbContext>();
        builder.Services.AddSingleton(_pricing.Object);
        builder.Services.AddSingleton(_stock.Object);

        var app = builder.Build();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static void RemoveApplicationHostedServices(IServiceCollection services)
    {
        var hostedServices = services
            .Where(item => item.ServiceType == typeof(IHostedService) &&
                item.ImplementationType?.Namespace?.StartsWith("ByeMoney", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var descriptor in hostedServices)
        {
            services.Remove(descriptor);
        }
    }

    private async Task<SeedData> SeedAsync()
    {
        var buyer = User.CreateInitial("http-buyer");
        var plain = User.CreateInitial("http-plain");
        var viewAdmin = User.CreateInitial("http-view-admin");
        var manageAdmin = User.CreateInitial("http-manage-admin");
        var viewRole = Role.Create("PurchasesViewOnly");
        var manageRole = Role.Create("CoursesManageOnly");

        var cart = Cart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, [
            new CartItemInput("book", ProductItemType.Product, 2),
            new CartItemInput("pen", ProductItemType.Product, 1)
        ], null);
        cart.SetQuote([
            new CartQuoteItem("book", "Book", 50, 0, 0, 100, ProductItemType.Product, null, 2),
            new CartQuoteItem("pen", "Pen", 20, 0, 0, 20, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 10);
        cart.MarkPaid();
        var book = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), cart.Id, buyer.Id, "book", "Book", 2, 50, 100);
        book.MarkDebited(cart.Id);
        var pen = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), cart.Id, buyer.Id, "pen", "Pen", 1, 20, 20);
        pen.MarkDebited(cart.Id);
        var unpaid = Cart.Create(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, ["unpaid"], null);
        unpaid.SetQuote([new CartQuoteItem("unpaid", "Unpaid", 10, 0, 0, 10)]);

        var account = Account.CreateUserAccount(buyer.Id);
        var wallet = Wallet.Create(account.Id, buyer.Id);
        wallet.ApplyCredit(500m);

        await using var db = new ApplicationDbContext(_options);
        db.AddRange(buyer, plain, viewAdmin, manageAdmin, viewRole, manageRole, cart, unpaid, book, pen, account, wallet);
        await db.SaveChangesAsync();
        db.AddRange(
            RolePermission.Create(viewRole.Id, PermissionConfiguration.PurchasesViewPermissionId),
            RolePermission.Create(manageRole.Id, PermissionConfiguration.CoursesManagePermissionId),
            UserRole.Create(viewAdmin.Id, viewRole.Id),
            UserRole.Create(manageAdmin.Id, manageRole.Id));
        await db.SaveChangesAsync();

        var ledgerCount = await db.Set<LedgerEntry>().CountAsync();
        return new SeedData(
            buyer.ExternalUserId, plain.ExternalUserId, viewAdmin.ExternalUserId, manageAdmin.ExternalUserId,
            buyer.Id.Value, cart.Id, unpaid.Id, book.Id.Value, wallet.WithdrawableBalance, ledgerCount);
    }

    private static string Token(string documentId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("id", "1"), new Claim("documentId", documentId)]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static string[] PropertyNames(JsonElement element)
    {
        return element.EnumerateObject().Select(property => property.Name).ToArray();
    }

    private sealed record CancellationSeed(
        string Buyer,
        string OtherUser,
        string CoursesManageAdmin,
        string NoorInjectAdmin,
        Guid BuyerId,
        Guid BuyerCartId,
        Guid AdminPurchaseId,
        CancellationState StartState);

    private sealed record CancellationState(decimal Balance, int LedgerCount, int CancellationRows);

    private sealed record SeedData(
        string Buyer,
        string PlainUser,
        string PurchasesViewAdmin,
        string CoursesManageAdmin,
        Guid BuyerId,
        Guid CartId,
        Guid UnpaidCartId,
        Guid BookId,
        decimal WalletBalance,
        int LedgerCount);
}
