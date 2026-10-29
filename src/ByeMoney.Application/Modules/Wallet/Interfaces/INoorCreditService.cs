using ByeMoney.Application.Modules.Wallet.DTOs;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface INoorCreditService
{
    Task<NoorCreditResult> CreditAsync(NoorCreditRequest request, CancellationToken ct = default);
}

