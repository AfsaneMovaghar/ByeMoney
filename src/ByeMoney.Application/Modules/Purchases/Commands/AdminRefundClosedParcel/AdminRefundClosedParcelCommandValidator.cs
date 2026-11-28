using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminRefundClosedParcel;

public sealed class AdminRefundClosedParcelCommandValidator : AbstractValidator<AdminRefundClosedParcelCommand>
{
    public AdminRefundClosedParcelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.PurchaseId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidPurchaseId);

        RuleFor(x => x.OperationId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidOperationId);

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage(PhysicalCancellationErrors.InvalidReason)
            .Must(reason => reason.Trim().Length <= ClosedParcelRefund.ReasonMaxLength)
            .WithMessage(PhysicalCancellationErrors.InvalidReason);
    }
}
