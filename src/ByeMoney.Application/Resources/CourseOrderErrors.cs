using System.Resources;

namespace ByeMoney.Application.Resources;

public static class CourseOrderErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.CourseOrderErrors", typeof(CourseOrderErrors).Assembly);

    public static string NotFound => Get(nameof(NotFound));
    public static string InvalidOrderId => Get(nameof(InvalidOrderId));
    public static string InvalidSourceType => Get(nameof(InvalidSourceType));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
