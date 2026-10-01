using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;

public record CreateAdminCardToCardTopUpCommand(
    string BeneficiaryExternalUserId,
    Guid ActorUserId,
    decimal AmountToman,
    string ReceiptId,
    string IdempotencyKey,
    string? ExternalTransactionId,
    ChargeType ChargeType) : IRequest<Result<CreateTopUpRequestResponse>>;
