using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.RefreshPhysicalShipment;

public sealed class RefreshPhysicalShipmentCommandValidator : AbstractValidator<RefreshPhysicalShipmentCommand>
{
    public RefreshPhysicalShipmentCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.PurchaseId)
            .NotEmpty()
            .WithMessage(OrderErrors.InvalidPurchaseId);
    }
}
