using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;

public class CreateAdminCardToCardTopUpCommandValidator : AbstractValidator<CreateAdminCardToCardTopUpCommand>
{
    public CreateAdminCardToCardTopUpCommandValidator(IReceiptStorage receipts)
    {
        RuleFor(x => x.BeneficiaryExternalUserId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.User_ExternalUserIdRequired)
            .MaximumLength(200).WithMessage(ApplicationErrors.TopUpRequest_BeneficiaryIdMaxLength);
        RuleFor(x => x.ActorUserId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_UserIdRequired);
        RuleFor(x => x.AmountToman).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeGreaterThanZero);
        RuleFor(x => x.ReceiptId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ReceiptRequired)
            .MaximumLength(200).WithMessage(ApplicationErrors.TopUpRequest_ReceiptIdMaxLength)
            .MustAsync((id, ct) => receipts.ExistsAsync(id, ct)).WithMessage(ApplicationErrors.TopUpRequest_ReceiptNotFound);
        RuleFor(x => x.IdempotencyKey).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_IdempotencyKeyRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_IdempotencyKeyMaxLength);
        RuleFor(x => x.ExternalTransactionId).Cascade(CascadeMode.Stop)
            .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdMaxLength);
        RuleFor(x => x.ChargeType).Cascade(CascadeMode.Stop)
            .IsInEnum().WithMessage(ApplicationErrors.TopUpRequest_UnsupportedChargeType)
            .Must(x => x switch
            {
                ChargeType.AdminAssistedCardToCard => true,
                _ => false
            }).WithMessage(ApplicationErrors.TopUpRequest_UnsupportedChargeType);
    }
}
