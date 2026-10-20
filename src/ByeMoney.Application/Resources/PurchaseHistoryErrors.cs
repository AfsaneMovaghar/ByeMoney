using System.Resources;

namespace ByeMoney.Application.Resources;

public static class PurchaseHistoryErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.PurchaseHistoryErrors", typeof(PurchaseHistoryErrors).Assembly);

    public static string NotFound => Get(nameof(NotFound));
    public static string InvalidPage => Get(nameof(InvalidPage));
    public static string InvalidBuyer => Get(nameof(InvalidBuyer));
    public static string InvalidDateRange => Get(nameof(InvalidDateRange));
    public static string InvalidStatus => Get(nameof(InvalidStatus));
    public static string InvalidPurchaseId => Get(nameof(InvalidPurchaseId));
    public static string InvalidFilter => Get(nameof(InvalidFilter));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
