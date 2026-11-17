using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;

public sealed class ResolveCarrierReturnCommandValidator : AbstractValidator<ResolveCarrierReturnCommand>
{
    public ResolveCarrierReturnCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.PurchaseId)
            .NotEmpty()
            .WithMessage(OrderErrors.InvalidPurchaseId);

        RuleFor(command => command.Resolution)
            .NotEmpty()
            .WithMessage(OrderErrors.CarrierReturn_InvalidResolution)
            .Must(BeValidResolution)
            .WithMessage(OrderErrors.CarrierReturn_InvalidResolution);

        RuleFor(command => command.Note)
            .NotEmpty()
            .WithMessage(OrderErrors.CarrierReturn_NoteRequired)
            .MaximumLength(PhysicalProductPurchase.CarrierReturnResolutionNoteMaxLength)
            .WithMessage(OrderErrors.CarrierReturn_NoteTooLong);
    }

    private static bool BeValidResolution(string? resolution) =>
        Enum.TryParse<CarrierReturnResolution>(resolution, true, out var parsed) &&
        Enum.IsDefined(parsed);
}

