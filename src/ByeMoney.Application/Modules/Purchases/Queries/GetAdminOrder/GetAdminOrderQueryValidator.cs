using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrder;

public sealed class GetAdminOrderQueryValidator : AbstractValidator<GetAdminOrderQuery>
{
    public GetAdminOrderQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(OrderIdFormat.IsValid).WithMessage(OrderErrors.InvalidOrderId);
    }
}
