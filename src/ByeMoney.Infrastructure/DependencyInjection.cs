using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Infrastructure.Modules.Identity.Persistence.User;
using ByeMoney.Infrastructure.Modules.Identity.Services;
using ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Modules.Settings.Persistence;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence;
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
        services.AddScoped<ICoursePurchaseRepository, CoursePurchaseRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddHostedService<CoursePurchaseNotificationRetryBackgroundService>();
        services.AddWalletModule();
        services.AddTarhElahiIntegration(configuration);
        return services;
    }
}
