using ByeMoney.Domain.Modules.Wallet.Gifts;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IGiftCampaignSettingRepository
{
    Task<GiftCampaignSetting?> GetByTypeAsync(GiftCampaignType type, CancellationToken ct = default);
    Task AddAsync(GiftCampaignSetting setting, CancellationToken ct = default);
    void Update(GiftCampaignSetting setting);
}

