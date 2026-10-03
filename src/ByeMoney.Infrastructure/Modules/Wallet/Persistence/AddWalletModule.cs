using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation;
using ByeMoney.Domain.Common;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using MediatR;
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
        services.AddScoped<IRequestHandler<ConfirmGatewayTopUpCommand, GatewayConfirmationOutcome>, ConfirmGatewayTopUpCommandHandler>();
        services.AddScoped<IRequestHandler<ReportGatewayCancellationCommand, Result>, ReportGatewayCancellationCommandHandler>();
        return services;
    }
}

