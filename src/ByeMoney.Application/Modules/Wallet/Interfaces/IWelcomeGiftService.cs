using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IWelcomeGiftService
{
    Task GrantWelcomeGiftIfEligibleAsync(User user, CancellationToken ct = default);
}

