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

        RuleFor(command => command.CollectionType)
            .Must(IsAllowedCollectionType)
            .WithMessage(OrderErrors.ShipmentCollectionTypeRequired);
    }

    private static bool IsAllowedCollectionType(string? collectionType) =>
        string.Equals(collectionType?.Trim(), "pick_up", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(collectionType?.Trim(), "courier_drop_off", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ||
        (PhysicalProductPurchase.AllowedTrackingProviders.Contains(
             provider.Trim(), StringComparer.OrdinalIgnoreCase) &&
         !string.Equals(provider.Trim(), PhysicalProductPurchase.ManualProvider,
             StringComparison.OrdinalIgnoreCase));
}
