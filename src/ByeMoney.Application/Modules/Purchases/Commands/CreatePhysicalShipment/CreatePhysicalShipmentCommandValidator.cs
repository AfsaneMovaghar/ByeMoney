using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

public sealed class CreatePhysicalShipmentCommandValidator : AbstractValidator<CreatePhysicalShipmentCommand>
{
    public CreatePhysicalShipmentCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.PurchaseId)
            .NotEmpty()
            .WithMessage(OrderErrors.InvalidPurchaseId);

        RuleFor(command => command.Provider)
            .Must(IsAllowedProvider)
            .WithMessage(OrderErrors.InvalidTrackingProvider);
    }

    private static bool IsAllowedProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ||
        (PhysicalProductPurchase.AllowedTrackingProviders.Contains(
             provider.Trim(), StringComparer.OrdinalIgnoreCase) &&
         !string.Equals(provider.Trim(), PhysicalProductPurchase.ManualProvider,
             StringComparison.OrdinalIgnoreCase));
}
