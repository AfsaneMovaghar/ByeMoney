using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreateCourseCartTopUp;

public sealed record CreateCourseCartTopUpCommand(Guid CartId) : IRequest<Result<CreateTopUpRequestResponse>>;
