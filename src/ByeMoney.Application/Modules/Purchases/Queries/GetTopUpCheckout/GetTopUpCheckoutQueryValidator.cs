using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetTopUpCheckout;

public sealed class GetTopUpCheckoutQueryValidator : AbstractValidator<GetTopUpCheckoutQuery>
{
    public GetTopUpCheckoutQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.TopUpId).NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_NotFound);
    }
}
