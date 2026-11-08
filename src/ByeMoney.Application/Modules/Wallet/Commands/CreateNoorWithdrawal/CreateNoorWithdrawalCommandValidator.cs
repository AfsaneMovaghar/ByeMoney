using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;

public sealed class CreateNoorWithdrawalCommandValidator : AbstractValidator<CreateNoorWithdrawalCommand>
{
    public CreateNoorWithdrawalCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.OperationId)
            .NotEmpty()
            .WithMessage(NoorWithdrawalErrors.InvalidOperationId);

        RuleFor(x => x.UserExternalId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidUser)
            .MaximumLength(100)
            .WithMessage(NoorWithdrawalErrors.InvalidUser);

        RuleFor(x => x.Kind)
            .Must(kind => NoorWithdrawalRules.TryParseKind(kind, out _))
            .WithMessage(NoorWithdrawalErrors.InvalidKind);

        RuleFor(x => x.RelatedTopUpId)
            .Must(id => id.HasValue && id.Value != Guid.Empty)
            .When(x => IsDuplicateRefund(x.Kind))
            .WithMessage(NoorWithdrawalErrors.TopUpRequired);

        RuleFor(x => x.RelatedTopUpId)
            .Null()
            .When(x => !IsDuplicateRefund(x.Kind))
            .WithMessage(NoorWithdrawalErrors.TopUpNotAllowed);

        RuleFor(x => x.AmountRial)
            .Must(NoorWithdrawalRules.IsPositiveWholeNumber)
            .WithMessage(NoorWithdrawalErrors.InvalidAmountRial);

        RuleFor(x => x.BeneficiaryName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidBeneficiaryName)
            .MaximumLength(NoorWithdrawal.BeneficiaryNameMaxLength)
            .WithMessage(NoorWithdrawalErrors.InvalidBeneficiaryName);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.BeneficiaryCardNumber) || !string.IsNullOrWhiteSpace(x.BeneficiaryIban))
            .WithMessage(NoorWithdrawalErrors.BeneficiaryAccountRequired);

        RuleFor(x => x.BeneficiaryCardNumber)
            .Must(NoorWithdrawalRules.IsValidCardNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.BeneficiaryCardNumber))
            .WithMessage(NoorWithdrawalErrors.InvalidCardNumber);

        RuleFor(x => x.BeneficiaryIban)
            .Must(NoorWithdrawalRules.IsValidIban)
            .When(x => !string.IsNullOrWhiteSpace(x.BeneficiaryIban))
            .WithMessage(NoorWithdrawalErrors.InvalidIban);

        RuleFor(x => x.Description)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidDescription)
            .MaximumLength(NoorWithdrawal.DescriptionMaxLength)
            .WithMessage(NoorWithdrawalErrors.InvalidDescription);

        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.PaymentReference) == string.IsNullOrWhiteSpace(x.ReceiptId))
            .WithMessage(NoorWithdrawalErrors.PaymentWithoutReference);

        RuleFor(x => x.PaymentReference)
            .MaximumLength(NoorWithdrawal.PaymentReferenceMaxLength)
            .WithMessage(NoorWithdrawalErrors.InvalidPaymentReference);
    }

    private static bool IsDuplicateRefund(string kind) =>
        NoorWithdrawalRules.TryParseKind(kind, out var parsed) && parsed == NoorWithdrawalKind.DuplicatePaymentRefund;
}
