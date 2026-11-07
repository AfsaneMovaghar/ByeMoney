using ByeMoney.Application.Modules.Purchases.Orders.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;

public sealed class GetAdminOrdersQueryValidator : AbstractValidator<GetAdminOrdersQuery>
{
    public GetAdminOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .DependentRules(() =>
            {
                RuleFor(query => query.Filter.ToFilter()).SetValidator(new OrderFilterValidator());
                RuleFor(query => query.Filter.BuyerExternalUserId)
                    .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
                    .WithMessage(PurchaseHistoryErrors.InvalidBuyer)
                    .MaximumLength(100).WithMessage(PurchaseHistoryErrors.InvalidBuyer);
            });
    }
}
