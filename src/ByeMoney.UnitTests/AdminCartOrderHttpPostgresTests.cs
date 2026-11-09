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
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Permissions;
using ByeMoney.Domain.Modules.Identity.Roles;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Modules.Identity.Persistence.Permission;
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
using Moq;
using Npgsql;

namespace ByeMoney.UnitTests;

/// <summary>
/// پذیرش HTTP سفارش دستی ادمین روی سبد (D2) با JWT امضاشدهٔ واقعی، RBAC واقعی و PostgreSQL؛
/// فقط quote و انتقال‌های استرپی شبیه‌سازی می‌شوند.
/// </summary>
public sealed class AdminCartOrderHttpPostgresTests : IAsyncLifetime
{
    private const string JwtSecret = "admin-cart-order-http-tests-secret-at-least-32-bytes";
    private const string JwtIssuer = "bye-money-tests-issuer";
    private const string JwtAudience = "bye-money-tests-audience";
    private const string Url = "/api/admin/carts/orders";
    private const string FreeReason = "جبران قطعی پخش زنده";
    private const decimal StartingBalance = 500m;

    private static readonly Dictionary<string, CartQuoteItem> Catalog = new()
    {
        ["course-a"] = new("course-a", "دوره الف", 300, 50, 0, 250),
        ["chapter-b"] = new("chapter-b", "فصل ب", 120, 0, 0, 120, ProductItemType.CourseChapter, "parent-course"),
        ["parent-x"] = new("parent-x", "دوره والد", 100, 0, 0, 100),
        ["chapter-y"] = new("chapter-y", "فصل والددار", 40, 0, 0, 40, ProductItemType.CourseChapter, "parent-x"),
        ["expensive"] = new("expensive", "دوره گران", 600, 0, 0, 600)
    };

    private readonly string _schema = "admin_cart_http_test_" + Guid.NewGuid().ToString("N");
    private readonly Mock<ICartPricingClient> _pricing = new();
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahi = new();
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
            .Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(new CartQuote(
                request.CartId,
                request.BuyerExternalUserId,
                request.CouponCode,
                request.Items.Select(item => Catalog[item.ExternalId]).ToArray())));
        _pricing
            .Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
    public async Task CallersWithoutCoursesManageAreRejectedWithoutCreatingCart()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = Client(app);
        var body = Body(Guid.NewGuid(), seed.Beneficiary, ["course-a"]);

        (await SendAsync(client, HttpMethod.Post, Url, null, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(client, HttpMethod.Post, Url, Token(seed.Beneficiary), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(client, HttpMethod.Post, Url, Token(seed.PurchasesViewAdmin), body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance, 0, 0, 0, 0));
    }

    [CartPostgresFact]
    public async Task PaidCourseAndChapterDebitTotalOnceAndRepeatedCartIdHasOneEffect()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = Client(app);
        var cartId = Guid.NewGuid();
        var body = Body(cartId, seed.Beneficiary, ["course-a", "chapter-b"]);

        var first = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), body);
        var second = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), body);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOf(second)).Should().Be(nameof(CartStatus.Paid));
        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance - 370, 2, 370, 0, 1));

        var changedMode = Body(cartId, seed.Beneficiary, ["course-a", "chapter-b"], isFree: true, freeReason: FreeReason);
        var conflict = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), changedMode);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance - 370, 2, 370, 0, 1));

        await using var db = new ApplicationDbContext(_options);
        var chapter = await db.Set<CoursePurchase>().AsNoTracking()
            .SingleAsync(purchase => purchase.Snapshot.ExternalProductId == "chapter-b");
        chapter.Snapshot.ItemType.Should().Be(ProductItemType.CourseChapter);
        chapter.Snapshot.ExternalParentId.Should().Be("parent-course");
        chapter.IsFree.Should().BeFalse();

        var details = await AdminDetailsAsync(client, seed, cartId);
        details.GetProperty("createdByAdminExternalUserId").GetString().Should().Be(seed.CoursesManageAdmin);
        details.GetProperty("adminFreeReason").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [CartPostgresFact]
    public async Task FreeOrderZeroesAllSharesStoresReasonAndAdminAndHidesThemFromBuyer()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = Client(app);
        var cartId = Guid.NewGuid();
        var body = Body(cartId, seed.Beneficiary, ["course-a", "chapter-b"], isFree: true, freeReason: FreeReason);

        var response = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            json.RootElement.GetProperty("priceInNoor").GetDecimal().Should().Be(0);
            json.RootElement.GetProperty("shippingAmountInNoor").GetDecimal().Should().Be(0);
            json.RootElement.GetProperty("originalTotalInNoor").GetDecimal().Should().Be(420);
        }

        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance, 2, 0, 0, 1));
        await using (var db = new ApplicationDbContext(_options))
        {
            var cart = await db.Set<Cart>().AsNoTracking().SingleAsync(item => item.Id == cartId);
            cart.AdminFreeReason.Should().Be(FreeReason);
            cart.CreatedByAdminUserId.Should().Be(seed.CoursesManageAdminId);
            cart.Items.Should().OnlyContain(item => item.AllocatedAmountInNoor == 0);
            var purchases = await db.Set<CoursePurchase>().AsNoTracking().ToListAsync();
            purchases.Should().OnlyContain(purchase =>
                purchase.IsFree &&
                purchase.FreeReason == FreeReason &&
                purchase.Status == CoursePurchaseStatus.NotificationSent &&
                purchase.RegularAmountInNoor == 0 &&
                purchase.GiftAmountInNoor == 0);
        }

        var details = await AdminDetailsAsync(client, seed, cartId);
        details.GetProperty("createdByAdminExternalUserId").GetString().Should().Be(seed.CoursesManageAdmin);
        details.GetProperty("adminFreeReason").GetString().Should().Be(FreeReason);

        var own = await SendAsync(client, HttpMethod.Get, $"/api/me/orders/cart:{cartId:D}", Token(seed.Beneficiary));
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownJson = await own.Content.ReadAsStringAsync();
        ownJson.Should().NotContain("createdByAdmin").And.NotContain("adminFreeReason").And.NotContain(FreeReason);
    }

    [CartPostgresFact]
    public async Task ShortfallIs409WithoutFinancialEffectReleasesReservationAndRetrySucceedsAfterTopUp()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = Client(app);
        var cartId = Guid.NewGuid();
        var body = Body(cartId, seed.Beneficiary, ["expensive"]);

        var shortfall = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), body);

        shortfall.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(shortfall)).Should().Be(PurchaseErrorCodes.InsufficientNoorBalance);
        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance, 0, 0, 0, 1));
        await using (var db = new ApplicationDbContext(_options))
        {
            var cart = await db.Set<Cart>().AsNoTracking().SingleAsync(item => item.Id == cartId);
            cart.Status.Should().Be(CartStatus.Failed);
            cart.Synchronized.Should().BeTrue();
            cart.CouponCode.Should().BeNull();
        }

        _pricing.Verify(x => x.TransitionAsync(
            It.Is<CartTransitionRequest>(request => request.CartId == cartId),
            "release",
            It.IsAny<CancellationToken>()), Times.Once);

        await CreditAsync(seed, 200m);
        var retry = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin), body);

        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOf(retry)).Should().Be(nameof(CartStatus.Paid));
        (await StateAsync(seed)).Should().Be(new MoneyState(StartingBalance + 200 - 600, 1, 600, 0, 1));
    }

    [CartPostgresFact]
    public async Task ChapterWhoseParentIsOwnedAndProductItemsAreRejectedWithoutEffect()
    {
        var seed = await SeedAsync();
        await using var app = await StartAppAsync();
        using var client = Client(app);
        var parent = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin),
            Body(Guid.NewGuid(), seed.Beneficiary, ["parent-x"]));
        parent.StatusCode.Should().Be(HttpStatusCode.OK);
        var before = await StateAsync(seed);

        var chapter = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin),
            Body(Guid.NewGuid(), seed.Beneficiary, ["chapter-y"], ProductItemType.CourseChapter));
        chapter.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(chapter)).Should().Be(CartErrorCodes.Conflict);

        var product = await SendAsync(client, HttpMethod.Post, Url, Token(seed.CoursesManageAdmin),
            Body(Guid.NewGuid(), seed.Beneficiary, ["book"], ProductItemType.Product));
        product.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = await StateAsync(seed);
        after.Should().Be(before with { Carts = before.Carts + 1 });
    }

    private static object Body(
        Guid cartId,
        string beneficiary,
        string[] ids,
        ProductItemType? type = null,
        bool isFree = false,
        string? freeReason = null)
    {
        var items = ids
            .Select(id => new
            {
                externalId = id,
                type = (type ?? (Catalog.TryGetValue(id, out var quote) ? quote.Type : ProductItemType.Course)).ToString(),
                quantity = 1
            })
            .ToArray();
        return new { cartId, beneficiaryExternalUserId = beneficiary, items, isFree, freeReason };
    }

    private async Task<JsonElement> AdminDetailsAsync(HttpClient client, SeedData seed, Guid cartId)
    {
        var response = await SendAsync(client, HttpMethod.Get, $"/api/admin/orders/cart:{cartId:D}", Token(seed.PurchasesViewAdmin));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static async Task<string?> StatusOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("status").GetString();
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private async Task<MoneyState> StateAsync(SeedData seed)
    {
        await using var db = new ApplicationDbContext(_options);
        var wallet = await db.Set<Wallet>().AsNoTracking().SingleAsync(item => item.UserId == new UserId(seed.BeneficiaryId));
        var purchaseLedger = await db.Set<LedgerEntry>().AsNoTracking()
            .Where(entry => entry.ReferenceType == LedgerReferenceType.Purchase)
            .ToListAsync();
        return new MoneyState(
            wallet.TotalBalance,
            await db.Set<CoursePurchase>().CountAsync(),
            -purchaseLedger.Where(entry => entry.Amount < 0).Sum(entry => entry.Amount),
            purchaseLedger.Sum(entry => entry.Amount),
            await db.Set<Cart>().CountAsync());
    }

    private async Task CreditAsync(SeedData seed, decimal amount)
    {
        await using var db = new ApplicationDbContext(_options);
        var wallet = await db.Set<Wallet>().SingleAsync(item => item.UserId == new UserId(seed.BeneficiaryId));
        wallet.Credit(amount, LedgerBalanceType.Regular);
        await db.SaveChangesAsync();
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(settings => settings.ValidateOnBuild = false);
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Strapi:JwtSecret"] = JwtSecret,
            ["Strapi:JwtIssuer"] = JwtIssuer,
            ["Strapi:JwtAudience"] = JwtAudience
        });
        builder.Services.AddPresentationServices(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminCartOrdersController).Assembly);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        // کارهای پس‌زمینهٔ واقعی نباید در آزمون اجرا شوند و دیتابیس باید همان schema اختصاصی آزمون باشد.
        RemoveApplicationHostedServices(builder.Services);
        builder.Services.AddSingleton(_options);
        builder.Services.AddScoped<ApplicationDbContext>();
        builder.Services.AddSingleton(_pricing.Object);
        builder.Services.AddSingleton(_tarhElahi.Object);

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
        var beneficiary = User.CreateInitial("admin-order-beneficiary");
        var viewAdmin = User.CreateInitial("admin-order-view-admin");
        var manageAdmin = User.CreateInitial("admin-order-manage-admin");
        var viewRole = Role.Create("PurchasesViewOnly");
        var manageRole = Role.Create("CoursesManageOnly");
        var account = Account.CreateUserAccount(beneficiary.Id);
        var wallet = Wallet.Create(account.Id, beneficiary.Id);
        wallet.Credit(StartingBalance, LedgerBalanceType.Regular);

        await using var db = new ApplicationDbContext(_options);
        db.AddRange(beneficiary, viewAdmin, manageAdmin, viewRole, manageRole, account, wallet);
        await db.SaveChangesAsync();
        db.AddRange(
            RolePermission.Create(viewRole.Id, PermissionConfiguration.PurchasesViewPermissionId),
            RolePermission.Create(manageRole.Id, PermissionConfiguration.CoursesManagePermissionId),
            UserRole.Create(viewAdmin.Id, viewRole.Id),
            UserRole.Create(manageAdmin.Id, manageRole.Id));
        await db.SaveChangesAsync();

        return new SeedData(
            beneficiary.ExternalUserId,
            beneficiary.Id.Value,
            viewAdmin.ExternalUserId,
            manageAdmin.ExternalUserId,
            manageAdmin.Id.Value);
    }

    private static HttpClient Client(WebApplication app) => new() { BaseAddress = new Uri(app.Urls.Single()) };

    private static string Token(string documentId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("id", "1"), new Claim("documentId", documentId)]),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
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

    private sealed record SeedData(
        string Beneficiary,
        Guid BeneficiaryId,
        string PurchasesViewAdmin,
        string CoursesManageAdmin,
        Guid CoursesManageAdminId);

    private sealed record MoneyState(decimal Balance, int Purchases, decimal Debit, decimal NetLedger, int Carts);
}
