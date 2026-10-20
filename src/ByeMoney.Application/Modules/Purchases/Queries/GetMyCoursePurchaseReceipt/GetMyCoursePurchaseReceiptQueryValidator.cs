using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchaseReceipt;

public sealed class GetMyCoursePurchaseReceiptQueryValidator : AbstractValidator<GetMyCoursePurchaseReceiptQuery>
{
    public GetMyCoursePurchaseReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PurchaseId).NotEmpty().WithMessage(PurchaseHistoryErrors.InvalidPurchaseId);
    }
}
