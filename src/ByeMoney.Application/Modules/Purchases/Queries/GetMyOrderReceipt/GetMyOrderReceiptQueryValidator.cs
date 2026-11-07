using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrderReceipt;

public sealed class GetMyOrderReceiptQueryValidator : AbstractValidator<GetMyOrderReceiptQuery>
{
    public GetMyOrderReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(OrderIdFormat.IsValid).WithMessage(OrderErrors.InvalidOrderId);
    }
}
