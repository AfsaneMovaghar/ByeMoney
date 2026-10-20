using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ByeMoney.API;
using ByeMoney.API.Controllers;
using ByeMoney.API.Middleware;
using ByeMoney.Application;
using ByeMoney.Application.Modules.Purchases.Queries.History;
using ByeMoney.Application.Modules.Purchases.Queries.Orders;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Permissions;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ByeMoney.UnitTests;

internal static class PurchaseHistoryHttpChecks
{
    public static async Task VerifyAsync(
        DbContextOptions<ApplicationDbContext> options, Guid buyerId, Guid freeId, Guid otherId)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(settings => settings.ValidateOnBuild = false);
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Strapi:JwtSecret"] = "purchase-history-tests-only-secret-at-least-32-bytes"
        });
        builder.Services.AddPresentationServices(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(MyCoursePurchasesController).Assembly);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<ApplicationDbContext>();
        builder.Services.AddScoped<IPurchaseHistoryReader, PurchaseHistoryReader>();
        builder.Services.AddScoped<ICourseOrderReader, CourseOrderReader>();
        builder.Services.AddSingleton<IClaimsTransformation, TestClaimsTransformation>();
        builder.Services.AddAuthentication(authentication =>
        {
            authentication.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
            authentication.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            authentication.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

        await using var app = builder.Build();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        try
        {
            (await client.GetAsync("/api/me/course-purchases")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await client.GetAsync($"/api/admin/course-purchases/{otherId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Add("X-Test-Buyer", buyerId.ToString());
            var mine = await client.GetAsync("/api/me/course-purchases?page=1&pageSize=2");
            mine.StatusCode.Should().Be(HttpStatusCode.OK);
            using var page = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
            page.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
            page.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
            page.RootElement.GetProperty("items")[0].TryGetProperty("buyerExternalUserId", out _).Should().BeFalse();

            var free = await client.GetAsync($"/api/me/course-purchases/{freeId}");
            free.StatusCode.Should().Be(HttpStatusCode.OK);
            using var receipt = JsonDocument.Parse(await free.Content.ReadAsStringAsync());
            receipt.RootElement.GetProperty("financialStatus").GetString().Should().Be("Paid");
            receipt.RootElement.GetProperty("finalAmountInNoor").GetDecimal().Should().Be(0);
            receipt.RootElement.GetProperty("referenceRialPerNoorAtPurchaseTime").ValueKind.Should().Be(JsonValueKind.Null);
            var foreign = await client.GetAsync($"/api/me/course-purchases/{otherId}");
            var missing = await client.GetAsync($"/api/me/course-purchases/{Guid.NewGuid()}");
            foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await foreign.Content.ReadAsStringAsync()).Should().Be(await missing.Content.ReadAsStringAsync());

            (await client.GetAsync("/api/admin/course-purchases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/admin/course-purchases/{otherId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            client.DefaultRequestHeaders.Add("X-Test-Permission", Permissions.Courses.Manage);
            (await client.GetAsync("/api/admin/course-purchases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/admin/course-purchases/{otherId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            client.DefaultRequestHeaders.Remove("X-Test-Permission");
            client.DefaultRequestHeaders.Add("X-Test-Permission", Permissions.Purchases.View);
            var adminReceipt = await client.GetAsync($"/api/admin/course-purchases/{otherId}");
            adminReceipt.StatusCode.Should().Be(HttpStatusCode.OK);
            using var adminReceiptBody = JsonDocument.Parse(await adminReceipt.Content.ReadAsStringAsync());
            adminReceiptBody.RootElement.GetProperty("purchaseId").GetGuid().Should().Be(otherId);
            (await client.GetAsync($"/api/me/course-purchases/{otherId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"/api/admin/course-purchases/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"/api/admin/course-purchases/{Guid.Empty}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var admin = await client.GetAsync("/api/admin/course-purchases");
            admin.StatusCode.Should().Be(HttpStatusCode.OK);
            using var adminPage = JsonDocument.Parse(await admin.Content.ReadAsStringAsync());
            adminPage.RootElement.GetProperty("page").GetInt32().Should().Be(1);
            adminPage.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20);
            adminPage.RootElement.GetProperty("totalCount").GetInt32().Should().Be(4);
            var filtered = await client.GetAsync("/api/admin/course-purchases?buyerExternalUserId=history-buyer" +
                "&fromUtc=2026-10-19T10:01:00Z&toUtc=2026-10-19T10:02:00Z" +
                "&financialStatus=Paid&deliveryStatus=Delivered");
            filtered.StatusCode.Should().Be(HttpStatusCode.OK);
            using var filteredPage = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
            filteredPage.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
            (await client.GetAsync("/api/admin/course-purchases?deliveryStatus=3")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await client.GetAsync("/api/me/course-purchases?pageSize=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await VerifyOrdersAsync(client, otherId);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task VerifyOrdersAsync(HttpClient client, Guid otherPurchaseId)
    {
        var mine = await client.GetAsync("/api/me/course-orders");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        using var page = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        page.RootElement.GetProperty("totalCount").GetInt32().Should().Be(4);
        var items = page.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().Contain(item => item.GetProperty("financialStatus").GetString() == "Processing");
        items.Should().Contain(item => item.GetProperty("sourceType").GetString() == "Cart");
        foreach (var item in items)
        {
            var orderId = item.GetProperty("orderId").GetString();
            var response = await client.GetAsync($"/api/me/course-orders/{orderId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContain("notificationFailureReason");
            json.Should().NotContain("buyerExternalUserId");
        }

        var foreign = await client.GetAsync($"/api/me/course-orders/purchase:{otherPurchaseId:D}");
        var missing = await client.GetAsync($"/api/me/course-orders/purchase:{Guid.NewGuid():D}");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var foreignBody = JsonDocument.Parse(await foreign.Content.ReadAsStringAsync());
        using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        foreignBody.RootElement.GetProperty("errors").GetRawText().Should()
            .Be(missingBody.RootElement.GetProperty("errors").GetRawText());
        (await client.GetAsync("/api/me/course-orders/cart:invalid")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/me/course-orders?financialStatus=1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/me/course-orders?deliveryStatus=Failed")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/me/course-orders?sourceType=1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/me/course-orders?pageSize=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/me/course-orders?fromUtc=invalid")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var filtered = await client.GetAsync("/api/me/course-orders?financialStatus=Processing&sourceType=Purchase");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        using var filteredPage = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        filteredPage.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);

        client.DefaultRequestHeaders.Remove("X-Test-Permission");
        (await client.GetAsync("/api/admin/course-orders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Add("X-Test-Permission", Permissions.Courses.Manage);
        (await client.GetAsync("/api/admin/course-orders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-Test-Permission");
        client.DefaultRequestHeaders.Add("X-Test-Permission", Permissions.Purchases.View);
        var admin = await client.GetAsync("/api/admin/course-orders?buyerExternalUserId=history-other");
        admin.StatusCode.Should().Be(HttpStatusCode.OK);
        using var adminPage = JsonDocument.Parse(await admin.Content.ReadAsStringAsync());
        adminPage.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        adminPage.RootElement.GetProperty("items")[0].GetProperty("buyerExternalUserId")
            .GetString().Should().Be("history-other");
        client.DefaultRequestHeaders.Remove("X-Test-Buyer");
        (await client.GetAsync("/api/me/course-orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/admin/course-orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed class TestClaimsTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PurchaseHistoryTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Buyer"], out var buyerId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var claims = new List<Claim> { new(AppClaimTypes.InternalUserId, buyerId.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
            {
                claims.Add(new Claim(AppClaimTypes.Permission, permission.ToString()));
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
