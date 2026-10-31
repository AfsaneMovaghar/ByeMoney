using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreateCartTopUp;

public sealed record CreateCartTopUpCommand(Guid CartId) : IRequest<Result<CreateTopUpRequestResponse>>;
