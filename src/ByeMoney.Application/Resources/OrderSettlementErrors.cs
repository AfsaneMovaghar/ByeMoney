using System.Resources;

namespace ByeMoney.Application.Resources;

public static class OrderSettlementErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.OrderSettlementErrors", typeof(OrderSettlementErrors).Assembly);

    public static string Empty => Get(nameof(Empty));
    public static string PreviewChanged => Get(nameof(PreviewChanged));
    public static string AlreadySettled => Get(nameof(AlreadySettled));
    public static string PeriodNotFound => Get(nameof(PeriodNotFound));
    public static string InvalidPeriodId => Get(nameof(InvalidPeriodId));
    public static string InvalidTitle => Get(nameof(InvalidTitle));
    public static string InvalidNote => Get(nameof(InvalidNote));
    public static string InvalidPreviewToken => Get(nameof(InvalidPreviewToken));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
