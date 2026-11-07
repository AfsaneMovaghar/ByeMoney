using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;

public sealed class CloseOrderSettlementPeriodCommandValidator : AbstractValidator<CloseOrderSettlementPeriodCommand>
{
    public CloseOrderSettlementPeriodCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Title)
            .MaximumLength(OrderSettlementPeriod.TitleMaxLength)
            .WithMessage(OrderSettlementErrors.InvalidTitle);

        RuleFor(command => command.Note)
            .MaximumLength(OrderSettlementPeriod.NoteMaxLength)
            .WithMessage(OrderSettlementErrors.InvalidNote);

        RuleFor(command => command.PreviewToken)
            .NotEmpty()
            .WithMessage(OrderSettlementErrors.InvalidPreviewToken)
            .Length(OrderSettlementPreviewToken.Length)
            .WithMessage(OrderSettlementErrors.InvalidPreviewToken);
    }
}
