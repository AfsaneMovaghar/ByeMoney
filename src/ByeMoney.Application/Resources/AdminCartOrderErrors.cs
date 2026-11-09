using System.Resources;

namespace ByeMoney.Application.Resources;

public static class AdminCartOrderErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.AdminCartOrderErrors", typeof(AdminCartOrderErrors).Assembly);

    public static string ItemTypeNotAllowed => Get(nameof(ItemTypeNotAllowed));
    public static string FreeReasonRequired => Get(nameof(FreeReasonRequired));
    public static string FreeReasonNotAllowed => Get(nameof(FreeReasonNotAllowed));
    public static string InsufficientNoorBalance => Get(nameof(InsufficientNoorBalance));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
