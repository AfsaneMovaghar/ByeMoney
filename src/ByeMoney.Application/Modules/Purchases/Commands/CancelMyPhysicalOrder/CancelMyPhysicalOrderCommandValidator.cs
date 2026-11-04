using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;

public sealed class CancelMyPhysicalOrderCommandValidator : AbstractValidator<CancelMyPhysicalOrderCommand>
{
    public CancelMyPhysicalOrderCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidCartId);

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
