namespace ByeMoney.Application.Modules.Wallet.DTOs;

public sealed record NoorCreditResult(
    Guid TransactionId,
    decimal AmountCredited,
    decimal NewWithdrawableBalance,
    decimal NewGiftBalance,
    ProvisionedUserWallet UserWallet
);

