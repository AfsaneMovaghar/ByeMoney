namespace ByeMoney.API.Authentication;

/// <summary>
/// سرویس فراخواننده مسیرهای نتیجه و بازیابی درگاه؛ فقط از روی کلید سرویس تطبیق‌خورده تعیین می‌شود.
/// </summary>
public enum GatewayCaller
{
    Strapi,
    Frontend
}
