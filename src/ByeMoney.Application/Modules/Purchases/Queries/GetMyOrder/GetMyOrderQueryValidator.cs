using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;

public sealed class GetMyOrderQueryValidator : AbstractValidator<GetMyOrderQuery>
{
    public GetMyOrderQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(OrderIdFormat.IsValid).WithMessage(OrderErrors.InvalidOrderId);
    }
}
