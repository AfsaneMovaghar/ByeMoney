using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;

public sealed record GatewayConfirmationOutcome(Result Result, bool Idempotent = false);
