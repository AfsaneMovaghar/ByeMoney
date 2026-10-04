using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.Account;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.TopUp;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.Wallet;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence;

public static class WalletModule
{
    public static IServiceCollection AddWalletModule(this IServiceCollection services)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITopUpRequestRepository, TopUpRequestRepository>();
        services.AddScoped<IGatewayTopUpService, GatewayTopUpService>();
        services.AddScoped<ITopUpRejectionService, TopUpRejectionService>();
        return services;
    }
}
