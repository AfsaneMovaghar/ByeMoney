using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.Account;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.TopUp;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.Wallet;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence;

public static class WalletModule
{
    public static IServiceCollection AddWalletModule(this IServiceCollection services)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITopUpRequestRepository, TopUpRequestRepository>();
        return services;
    }
}

