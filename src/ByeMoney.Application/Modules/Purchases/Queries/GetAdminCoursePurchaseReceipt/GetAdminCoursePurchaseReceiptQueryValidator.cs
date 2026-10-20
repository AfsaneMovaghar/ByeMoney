using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchaseReceipt;

public sealed class GetAdminCoursePurchaseReceiptQueryValidator : AbstractValidator<GetAdminCoursePurchaseReceiptQuery>
{
    public GetAdminCoursePurchaseReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PurchaseId).NotEmpty().WithMessage(PurchaseHistoryErrors.InvalidPurchaseId);
    }
}
