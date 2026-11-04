using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;

public sealed class AdminCancelPhysicalPurchaseCommandValidator : AbstractValidator<AdminCancelPhysicalPurchaseCommand>
{
    public AdminCancelPhysicalPurchaseCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.PurchaseId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidPurchaseId);

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage(PhysicalCancellationErrors.InvalidQuantity);

        RuleFor(x => x.OperationId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidOperationId);

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage(PhysicalCancellationErrors.InvalidReason)
            .MaximumLength(500)
            .WithMessage(PhysicalCancellationErrors.InvalidReason);
    }
}
