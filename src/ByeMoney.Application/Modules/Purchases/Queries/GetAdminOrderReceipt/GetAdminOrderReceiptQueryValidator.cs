using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrderReceipt;

public sealed class GetAdminOrderReceiptQueryValidator : AbstractValidator<GetAdminOrderReceiptQuery>
{
    public GetAdminOrderReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(OrderIdFormat.IsValid).WithMessage(OrderErrors.InvalidOrderId);
    }
}
