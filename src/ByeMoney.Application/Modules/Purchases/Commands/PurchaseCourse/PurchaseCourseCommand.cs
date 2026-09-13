using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;

public record PurchaseCourseCommand(
    Guid BuyerUserId,
    string ExternalCourseId) : IRequest<Result<PurchaseCourseResponse>>;
