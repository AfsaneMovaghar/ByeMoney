using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

public sealed class CreatePhysicalShipmentCommandValidator : AbstractValidator<CreatePhysicalShipmentCommand>
{
    public CreatePhysicalShipmentCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.CartId)
            .NotEmpty()
            .WithMessage(OrderErrors.InvalidCartId);

        RuleFor(command => command.BoxTypeId)
            .Must(IsAllowedBoxType)
            .WithMessage(OrderErrors.InvalidBoxTypeId);

        RuleFor(command => command.Provider)
            .Must(IsAllowedProvider)
            .WithMessage(OrderErrors.InvalidTrackingProvider);
    }

    private static bool IsAllowedBoxType(int? boxTypeId) =>
        boxTypeId is null ||
        (boxTypeId.Value >= ParcelFacts.MinBoxTypeId && boxTypeId.Value <= ParcelFacts.MaxBoxTypeId);

    private static bool IsAllowedProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ||
        (PhysicalProductPurchase.AllowedTrackingProviders.Contains(
             provider.Trim(), StringComparer.OrdinalIgnoreCase) &&
         !string.Equals(provider.Trim(), PhysicalProductPurchase.ManualProvider,
             StringComparison.OrdinalIgnoreCase));
}
