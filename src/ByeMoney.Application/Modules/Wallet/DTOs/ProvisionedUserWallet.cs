using ByeMoney.Domain.Modules.Wallet.Accounts;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.DTOs;

public record ProvisionedUserWallet(Account Account, WalletEntity Wallet);
