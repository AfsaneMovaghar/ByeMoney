using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;

public sealed class UpdatePhysicalDeliveryStatusCommandValidator : AbstractValidator<UpdatePhysicalDeliveryStatusCommand>
{
    public UpdatePhysicalDeliveryStatusCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.PurchaseId)
            .NotEmpty()
            .WithMessage(CourseOrderErrors.InvalidPurchaseId);

        RuleFor(command => command.Status)
            .Must(IsAllowedTarget)
            .WithMessage(CourseOrderErrors.InvalidDeliveryStatus);
    }

    private static bool IsAllowedTarget(string? status) =>
        status is nameof(PhysicalDeliveryStatus.Shipped) or nameof(PhysicalDeliveryStatus.Delivered);
}
