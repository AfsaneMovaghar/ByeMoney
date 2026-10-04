using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;

public sealed class RecordGatewayResultCommandValidator : AbstractValidator<RecordGatewayResultCommand>
{
    public RecordGatewayResultCommandValidator()
    {
        RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeRequired).MaximumLength(32)
            .WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);
        RuleFor(x => x.Gateway).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayRequired).MaximumLength(30)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayMaxLength);
        RuleFor(x => x.EventId).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayEventIdRequired).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayEventIdMaxLength);
        RuleFor(x => x.Kind).Cascade(CascadeMode.Stop).Must(x => x is
            GatewayResultKinds.Unpaid or GatewayResultKinds.Verified or GatewayResultKinds.ReverseSucceeded
                or GatewayResultKinds.ReverseFailed or GatewayResultKinds.Unknown)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayResultKindInvalid);
        RuleFor(x => x.BankTransactionId).Cascade(CascadeMode.Stop).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdMaxLength);
        RuleFor(x => x.BankReferenceNumber).Cascade(CascadeMode.Stop).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberMaxLength);
        RuleFor(x => x.BankResultCode).Cascade(CascadeMode.Stop).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdMaxLength);
        RuleFor(x => x.OccurredAtUtc).Cascade(CascadeMode.Stop).Must(x => x != default && x.Kind == DateTimeKind.Utc)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayOccurredAtUtcInvalid);
        When(x => x.Kind == GatewayResultKinds.Unpaid, () =>
            RuleFor(x => x.BankTransactionId).Cascade(CascadeMode.Stop).Empty()
                .WithMessage(ApplicationErrors.TopUpRequest_GatewayUnpaidWithBankId));
        When(x => x.Kind is GatewayResultKinds.Verified or GatewayResultKinds.ReverseSucceeded
            or GatewayResultKinds.ReverseFailed, () =>
        {
            RuleFor(x => x.BankTransactionId).Cascade(CascadeMode.Stop).NotEmpty()
                .WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdRequired);
        });
        When(x => x.Kind == GatewayResultKinds.Verified, () =>
        {
            RuleFor(x => x.BankReferenceNumber).Cascade(CascadeMode.Stop).NotEmpty()
                .WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberRequired);
            RuleFor(x => x.OriginalAmountRial).Cascade(CascadeMode.Stop).NotNull()
                .WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero)
                .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero);
            RuleFor(x => x.AffectiveAmountRial).Cascade(CascadeMode.Stop).NotNull()
                .WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero)
                .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero);
        });
    }
}
