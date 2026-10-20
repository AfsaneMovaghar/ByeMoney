namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public record WalletBalanceResponse(
    decimal Balance,
    string Currency = "Noor");
