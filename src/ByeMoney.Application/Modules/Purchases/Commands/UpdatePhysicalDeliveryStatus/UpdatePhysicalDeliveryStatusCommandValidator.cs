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
            .WithMessage(OrderErrors.InvalidPurchaseId);

        RuleFor(command => command.Status)
            .Must(IsAllowedTarget)
            .WithMessage(OrderErrors.InvalidDeliveryStatus);

        RuleFor(command => command.TrackingCode)
            .MaximumLength(PhysicalProductPurchase.TrackingCodeMaxLength)
            .WithMessage(OrderErrors.InvalidTrackingCode)
            .Must(ContainNoControlCharacters)
            .WithMessage(OrderErrors.InvalidTrackingCode);

        RuleFor(command => command.TrackingProvider)
            .Must(IsAllowedProvider)
            .WithMessage(OrderErrors.InvalidTrackingProvider);

        RuleFor(command => command.ProviderParcelId)
            .MaximumLength(PhysicalProductPurchase.ProviderParcelIdMaxLength)
            .WithMessage(OrderErrors.InvalidProviderParcelId)
            .Must(ContainNoControlCharacters)
            .WithMessage(OrderErrors.InvalidProviderParcelId);

        // شناسهٔ ارجاع فقط با منبع خودکار معنا دارد و منبع دستی آن را نگه نمی‌دارد؛
        // ورودی ناسازگار رد می‌شود تا حالت خاموش «استعلام انجام نشد» پیش نیاید.
        RuleFor(command => command.ProviderParcelId)
            .Must((command, providerParcelId) =>
                HasConsistentProviderReference(command.TrackingProvider, providerParcelId))
            .WithMessage(OrderErrors.InvalidProviderParcelId)
            .When(command => command.Status == nameof(PhysicalDeliveryStatus.Shipped));
    }

    private static bool IsAllowedProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ||
        PhysicalProductPurchase.AllowedTrackingProviders.Contains(provider.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool RequiresProviderParcelId(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) &&
        !string.Equals(provider.Trim(), PhysicalProductPurchase.ManualProvider, StringComparison.OrdinalIgnoreCase);

    private static bool HasConsistentProviderReference(string? provider, string? providerParcelId)
    {
        var hasReference = !string.IsNullOrWhiteSpace(providerParcelId);
        return RequiresProviderParcelId(provider) ? hasReference : !hasReference;
    }

    private static bool ContainNoControlCharacters(string? code) =>
        code is null || !code.Any(char.IsControl);

    private static bool IsAllowedTarget(string? status) =>
        status is nameof(PhysicalDeliveryStatus.Shipped) or nameof(PhysicalDeliveryStatus.Delivered);
}
