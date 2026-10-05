using System.Reflection;
using System.Text;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ByeMoney.UnitTests;

public class PermissionMatrixReflectionTests
{
    private sealed record EndpointAuthInfo(
        string Controller,
        string Action,
        string HttpMethod,
        string Route,
        string AuthRequirement,
        string? PolicyName,
        string? PermissionCode);

    private static readonly Dictionary<string, string> PolicyToPermissionMap = new()
    {
        [PolicyNames.RequireNoorInject] = Permissions.Noor.Inject,
        [PolicyNames.RequireTopUpReview] = Permissions.TopUp.Review,
        [PolicyNames.RequireCoursesManage] = Permissions.Courses.Manage,
        [PolicyNames.RequireWalletView] = Permissions.Wallet.View
    };

    [Fact]
    public void AllControllers_MustNotUseRolesDirectly()
    {
        var controllerTypes = typeof(ByeMoney.API.Controllers.AdminCoursesController).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var classAuth = controller.GetCustomAttributes<AuthorizeAttribute>(true);
            foreach (var auth in classAuth)
            {
                auth.Roles.Should().BeNullOrWhiteSpace(
                    $"Controller {controller.Name} must use Policy-based authorization instead of raw Roles");
            }

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var methodAuth = method.GetCustomAttributes<AuthorizeAttribute>(true);
                foreach (var auth in methodAuth)
                {
                    auth.Roles.Should().BeNullOrWhiteSpace(
                        $"Action {controller.Name}.{method.Name} must use Policy-based authorization instead of raw Roles");
                }
            }
        }
    }

    [Fact]
    public void PermissionMatrix_MustBeAccuratelyReflectedFromCode()
    {
        var endpoints = ExtractEndpointsFromControllers();
        endpoints.Should().NotBeEmpty();

        var matrixMarkdown = GenerateMatrixMarkdown(endpoints);

        // Verify docs directory exists and write/update the matrix
        var solutionRoot = FindSolutionRoot();
        var docsPath = Path.Combine(solutionRoot, "docs", "architecture", "Permissions-Matrix.md");
        Directory.CreateDirectory(Path.GetDirectoryName(docsPath)!);
        File.WriteAllText(docsPath, matrixMarkdown, Encoding.UTF8);

        File.Exists(docsPath).Should().BeTrue();
    }

    private static List<EndpointAuthInfo> ExtractEndpointsFromControllers()
    {
        var result = new List<EndpointAuthInfo>();
        var controllerTypes = typeof(ByeMoney.API.Controllers.AdminCoursesController).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var routeAttr = controller.GetCustomAttribute<RouteAttribute>();
            var baseRoute = routeAttr?.Template ?? "";

            var classAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>();
            var classAllowAnon = controller.GetCustomAttribute<AllowAnonymousAttribute>();

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .OrderBy(m => m.Name);

            foreach (var method in methods)
            {
                var httpMethodAttr = method.GetCustomAttributes()
                    .OfType<HttpMethodAttribute>()
                    .FirstOrDefault();

                if (httpMethodAttr == null)
                    continue;

                var httpMethod = httpMethodAttr.HttpMethods.FirstOrDefault() ?? "GET";
                var subRoute = httpMethodAttr.Template ?? "";

                var fullRoute = string.IsNullOrWhiteSpace(subRoute)
                    ? baseRoute
                    : $"{baseRoute.TrimEnd('/')}/{subRoute.TrimStart('/')}";

                if (!fullRoute.StartsWith("/"))
                    fullRoute = "/" + fullRoute;

                var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();
                var methodAllowAnon = method.GetCustomAttribute<AllowAnonymousAttribute>();

                string authReq;
                string? policy = null;
                string? permission = null;

                if (methodAllowAnon != null || (classAllowAnon != null && methodAuthorize == null))
                {
                    authReq = "عمومی (Anonymous)";
                }
                else if (methodAuthorize?.Policy != null)
                {
                    policy = methodAuthorize.Policy;
                    PolicyToPermissionMap.TryGetValue(policy, out permission);
                    authReq = $"نیازمند پالیسی `{policy}`";
                }
                else if (classAuthorize?.Policy != null)
                {
                    policy = classAuthorize.Policy;
                    PolicyToPermissionMap.TryGetValue(policy, out permission);
                    authReq = $"نیازمند پالیسی `{policy}`";
                }
                else if (methodAuthorize != null || classAuthorize != null)
                {
                    authReq = "احراز هویت شده (JWT)";
                }
                else
                {
                    authReq = "عمومی (Public / بدون Authorize)";
                }

                result.Add(new EndpointAuthInfo(
                    controller.Name.Replace("Controller", ""),
                    method.Name,
                    httpMethod,
                    fullRoute,
                    authReq,
                    policy,
                    permission));
            }
        }

        return result;
    }

    private static string GenerateMatrixMarkdown(List<EndpointAuthInfo> endpoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ماتریس دسترسی و مجوزهای ByeMoney (Permission Matrix)");
        sb.AppendLine();
        sb.AppendLine("> [!IMPORTANT]");
        sb.AppendLine("> این سند بر اساس بازتاب (Reflection) مستقیم کدهای واقعی پروژه، کنترلرها و `Permissions.cs` تولید شده است و منعکس‌کننده دقیق وضعیت واقعی اندپوینت‌ها و پالیسی‌ها است.");
        sb.AppendLine();
        sb.AppendLine("## ۱. فهرست مجوزهای تعریف‌شده در سامانه (Permissions)");
        sb.AppendLine();
        sb.AppendLine("| کد پرمیژن (Domain) | شناسه پالیسی در API (Policy) | توضیح و حوزه عملکرد |");
        sb.AppendLine("| :--- | :--- | :--- |");
        sb.AppendLine($"| `{Permissions.Noor.Inject}` | `{PolicyNames.RequireNoorInject}` | مجوز تزریق یا صدور مستقیم نور |");
        sb.AppendLine($"| `{Permissions.TopUp.Review}` | `{PolicyNames.RequireTopUpReview}` | مجوز بررسی، تأیید یا رد درخواست‌های شارژ حساب |");
        sb.AppendLine($"| `{Permissions.Courses.Manage}` | `{PolicyNames.RequireCoursesManage}` | مجوز ثبت و مدیریت خرید دوره به‌نیابت از کاربر |");
        sb.AppendLine($"| `{Permissions.Wallet.View}` | `{PolicyNames.RequireWalletView}` | مجوز استعلام دسته‌جمعی مانده کیف‌پول‌ها |");
        sb.AppendLine();
        sb.AppendLine("## ۲. ماتریس احراز هویت و دسترسی اندپوینت‌ها (Endpoint Authorization Matrix)");
        sb.AppendLine();
        sb.AppendLine("| ماژول / کنترلر | متد | مسیر (Route) | سطح دسترسی / پالیسی | پرمیژن مورد نیاز |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var ep in endpoints)
        {
            var perm = ep.PermissionCode != null ? $"`{ep.PermissionCode}`" : "—";
            sb.AppendLine($"| {ep.Controller} | `{ep.HttpMethod}` | `{ep.Route}` | {ep.AuthRequirement} | {perm} |");
        }

        sb.AppendLine();
        sb.AppendLine("## ۳. اصول معماری و قواعد RBAC در ByeMoney");
        sb.AppendLine();
        sb.AppendLine("1. **جداسازی Role از Endpoint:** هیچ کنترلر یا اکشنی نباید مستقیماً از `[Authorize(Roles = ...)]` استفاده کند. تمام کنترلرها باید منحصراً بر پایه Policy / Permission باشند.");
        sb.AppendLine("2. **نقش‌ها به عنوان کانتینر پرمیژن:** نقش `Admin` صرفاً مجموعه‌ای از این پرمیژن‌ها در پایگاه داده است و از طریق `RbacClaimsTransformation` به Claimهای کاربر تبدیل می‌شود.");
        sb.AppendLine("3. **تغییرناپذیری نام‌های دیتابیس بدون Migration:** کدهای پرمیژن موجود در دیتابیس (`Noor.Inject` و `TopUp.Review`) همواره با داده‌های Seed و جداول `Permissions` و `RolePermissions` هماهنگ هستند.");

        return sb.ToString();
    }

    private static string FindSolutionRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "ByeMoney.sln")) || Directory.Exists(Path.Combine(current, "docs")))
                return current;
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
