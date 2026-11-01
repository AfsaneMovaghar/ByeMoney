using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrderReceipt;

public sealed class GetMyCourseOrderReceiptQueryHandler(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCourseOrderReceiptQuery, CourseOrderReceipt>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<CourseOrderReceipt> Handle(GetMyCourseOrderReceiptQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var order = CourseOrderMapping.Build(snapshot)
            .SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details;

        if (order is null)
        {
            throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        }

        var receipt = CourseOrderReceiptMapping.Build(order, snapshot);
        return receipt ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
    }
}
