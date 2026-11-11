using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletTransactions;

public sealed class GetWalletTransactionsQueryValidator : AbstractValidator<GetWalletTransactionsQuery>
{
    public const int MaxPageSize = 100;

    public GetWalletTransactionsQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage(ApplicationErrors.Wallet_InvalidTransactionPage);

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage(ApplicationErrors.Wallet_InvalidTransactionPage)
            .Must((query, page) => query.PageSize is >= 1 and <= MaxPageSize && page <= int.MaxValue / query.PageSize)
            .WithMessage(ApplicationErrors.Wallet_InvalidTransactionPage);
    }
}
