namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public static class GatewayResultKinds
{
    public const string Unpaid = "Unpaid";
    public const string Verified = "Verified";
    public const string ReverseSucceeded = "ReverseSucceeded";
    public const string ReverseFailed = "ReverseFailed";
    public const string Unknown = "Unknown";
}
