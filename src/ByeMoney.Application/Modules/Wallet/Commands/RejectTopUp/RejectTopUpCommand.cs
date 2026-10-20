using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public record RejectTopUpCommand(
    Guid TopUpRequestId,
    string Reason) : IRequest<Result>;

