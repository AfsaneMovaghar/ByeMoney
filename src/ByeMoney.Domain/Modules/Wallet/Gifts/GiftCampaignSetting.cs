using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Wallet.Gifts;

public class GiftCampaignSetting : BaseEntity<Guid>
{
    public GiftCampaignType CampaignType { get; private set; }
    public decimal AmountNoor { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public UserId? UpdatedByAdminUserId { get; private set; }

    private GiftCampaignSetting() { }

    public static GiftCampaignSetting Create(
        GiftCampaignType campaignType,
        decimal amountNoor,
        bool isEnabled,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        UserId? adminUserId = null,
        DateTime? updatedAtUtc = null)
    {
        if (amountNoor < 0)
        {
            throw new DomainException(DomainErrors.GiftCampaign_AmountCannotBeNegative);
        }

        if (startDateUtc.HasValue && endDateUtc.HasValue && startDateUtc > endDateUtc)
        {
            throw new DomainException(DomainErrors.GiftCampaign_StartDateCannotBeAfterEndDate);
        }

        var now = updatedAtUtc ?? DateTime.UtcNow;

        return new GiftCampaignSetting
        {
            Id = Guid.NewGuid(),
            CampaignType = campaignType,
            AmountNoor = amountNoor,
            IsEnabled = isEnabled,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            UpdatedAtUtc = now,
            UpdatedByAdminUserId = adminUserId
        };
    }

    public bool IsActiveAt(DateTime utcNow)
    {
        if (!IsEnabled || AmountNoor <= 0)
        {
            return false;
        }

        if (StartDateUtc.HasValue && utcNow < StartDateUtc.Value)
        {
            return false;
        }

        if (EndDateUtc.HasValue && utcNow > EndDateUtc.Value)
        {
            return false;
        }

        return true;
    }

    public void Update(
        decimal amountNoor,
        bool isEnabled,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        UserId? adminUserId,
        DateTime utcNow)
    {
        if (amountNoor < 0)
        {
            throw new DomainException(DomainErrors.GiftCampaign_AmountCannotBeNegative);
        }

        if (startDateUtc.HasValue && endDateUtc.HasValue && startDateUtc > endDateUtc)
        {
            throw new DomainException(DomainErrors.GiftCampaign_StartDateCannotBeAfterEndDate);
        }

        AmountNoor = amountNoor;
        IsEnabled = isEnabled;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        UpdatedByAdminUserId = adminUserId;
        UpdatedAtUtc = utcNow;
    }
}

