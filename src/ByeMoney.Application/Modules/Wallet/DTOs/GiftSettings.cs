namespace ByeMoney.Application.Modules.Wallet.DTOs;

public class GiftSettings
{
    public const string SectionName = "GiftSettings";

    public int MaxUsersPerBatch { get; set; } = 100;
    public decimal MaxAmountNoorPerUser { get; set; } = 10000m;
}

