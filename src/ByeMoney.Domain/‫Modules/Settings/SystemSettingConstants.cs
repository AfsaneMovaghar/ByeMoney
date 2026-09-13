namespace ByeMoney.Domain.Modules.Settings;

public static class SystemSettingConstants
{
    public static class Tables
    {
        public const string SystemSettings = "SystemSettings";
    }

    public static class Keys
    {
        public const string RialToNoor = "ConversionRate:RialToNoor";
    }

    public static class Defaults
    {
        public const decimal RialToNoorRate = 1000m;
        public const string RialToNoorRateString = "1000";
        public const string RialToNoorDescription = "Rial to Noor conversion rate (Rials per 1 Noor).";
    }

    public static class Limits
    {
        public const int KeyMaxLength = 100;
        public const int ValueMaxLength = 500;
        public const int DescriptionMaxLength = 500;
    }
}

