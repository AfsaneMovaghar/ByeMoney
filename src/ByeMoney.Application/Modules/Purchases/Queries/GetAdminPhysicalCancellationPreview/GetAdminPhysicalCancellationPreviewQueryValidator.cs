using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminPhysicalCancellationPreview;

public sealed class GetAdminPhysicalCancellationPreviewQueryValidator :
    AbstractValidator<GetAdminPhysicalCancellationPreviewQuery>
{
    public GetAdminPhysicalCancellationPreviewQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.PurchaseId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidPurchaseId);

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage(PhysicalCancellationErrors.InvalidQuantity);
    }
}
