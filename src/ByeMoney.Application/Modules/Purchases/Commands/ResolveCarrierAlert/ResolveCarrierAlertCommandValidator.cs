using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierAlert;

public sealed class ResolveCarrierAlertCommandValidator : AbstractValidator<ResolveCarrierAlertCommand>
{
    public ResolveCarrierAlertCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.PurchaseId)
            .NotEmpty()
            .WithMessage(OrderErrors.InvalidPurchaseId);

        RuleFor(command => command.Resolution)
            .NotEmpty()
            .WithMessage(OrderErrors.CarrierAlert_InvalidResolution)
            .Must(BeValidResolution)
            .WithMessage(OrderErrors.CarrierAlert_InvalidResolution);

        RuleFor(command => command.Note)
            .NotEmpty()
            .WithMessage(OrderErrors.CarrierAlert_NoteRequired)
            .MaximumLength(PhysicalProductPurchase.CarrierReturnResolutionNoteMaxLength)
            .WithMessage(OrderErrors.CarrierAlert_NoteTooLong);
    }

    private static bool BeValidResolution(string? resolution) =>
        Enum.TryParse<CarrierReturnResolution>(resolution, true, out var parsed) &&
        Enum.IsDefined(parsed);
}
