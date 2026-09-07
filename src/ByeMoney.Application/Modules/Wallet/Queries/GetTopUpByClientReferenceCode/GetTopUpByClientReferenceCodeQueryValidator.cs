using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;

public class GetTopUpByClientReferenceCodeQueryValidator
    : AbstractValidator<GetTopUpByClientReferenceCodeQuery>
{
    public GetTopUpByClientReferenceCodeQueryValidator()
    {
        RuleFor(x => x.ClientReferenceCode)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeRequired);
    }
}
