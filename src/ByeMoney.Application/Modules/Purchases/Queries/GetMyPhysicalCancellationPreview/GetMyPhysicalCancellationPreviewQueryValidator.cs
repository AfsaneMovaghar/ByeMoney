using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyPhysicalCancellationPreview;

public sealed class GetMyPhysicalCancellationPreviewQueryValidator :
    AbstractValidator<GetMyPhysicalCancellationPreviewQuery>
{
    public GetMyPhysicalCancellationPreviewQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidCartId);
    }
}
