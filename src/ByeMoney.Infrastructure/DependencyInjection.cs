using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using ByeMoney.Infrastructure.Modules.Identity.Persistence.User;
using ByeMoney.Infrastructure.Modules.Identity.Services;
using ByeMoney.Infrastructure.Modules.PostexIntegration;
using ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Modules.Settings.Persistence;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(BaseRepository<,>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<ICoursePurchaseRepository, CoursePurchaseRepository>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.History.Interfaces.IPurchaseHistoryReader, PurchaseHistoryReader>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.Orders.Interfaces.IOrderReader, OrderReader>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddHostedService<CoursePurchaseNotificationRetryBackgroundService>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.Carts.Interfaces.ICartStore, CartStore>();
        services.AddHostedService<CartRecoveryBackgroundService>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces.IPhysicalCancellationStore, PhysicalCancellationStore>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces.IClosedParcelRefundStore, ClosedParcelRefundStore>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.Orders.Interfaces.IPhysicalDeliveryStore, PhysicalDeliveryStore>();
        services.AddScoped<ByeMoney.Application.Modules.Purchases.Settlements.Interfaces.IOrderSettlementStore, OrderSettlementStore>();
        services.AddScoped<
            ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces.INoorWithdrawalStore,
            ByeMoney.Infrastructure.Modules.Wallet.Persistence.Withdrawals.NoorWithdrawalStore>();
        services.AddSingleton(
            configuration.GetSection(PhysicalRestockSettings.SectionName).Get<PhysicalRestockSettings>()
            ?? new PhysicalRestockSettings());
        services.AddHostedService<PhysicalRestockRecoveryBackgroundService>();
        services.AddSingleton(
            configuration.GetSection(PhysicalDeliverySettings.SectionName).Get<PhysicalDeliverySettings>()
            ?? new PhysicalDeliverySettings());
        services.AddScoped<PhysicalDeliverySyncService>();
        services.AddHostedService<PhysicalDeliveryAutoConfirmBackgroundService>();
        services.AddHostedService<ExternalTrackingBackgroundService>();
        services.AddScoped<ByeMoney.Application.Common.Interfaces.IReceiptStorage>(_ => new PrivateReceiptStorage(configuration["ReceiptStorage:Path"] ?? Path.Combine(AppContext.BaseDirectory, "private-receipts")));
        services.AddWalletModule();
        services.AddTarhElahiIntegration(configuration);
        services.AddPostexIntegration(configuration);
        return services;
    }
}
