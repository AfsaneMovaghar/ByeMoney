using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Gifts;

public class GiftCampaignSettingRepository(ApplicationDbContext dbContext) : IGiftCampaignSettingRepository
{
    public Task<GiftCampaignSetting?> GetByTypeAsync(GiftCampaignType type, CancellationToken ct = default)
    {
        return dbContext.GiftCampaignSettings
            .FirstOrDefaultAsync(s => s.CampaignType == type, ct);
    }

    public async Task AddAsync(GiftCampaignSetting setting, CancellationToken ct = default)
    {
        await dbContext.GiftCampaignSettings.AddAsync(setting, ct);
    }

    public void Update(GiftCampaignSetting setting)
    {
        dbContext.GiftCampaignSettings.Update(setting);
    }
}

