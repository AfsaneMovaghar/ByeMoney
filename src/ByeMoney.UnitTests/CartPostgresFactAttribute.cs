namespace ByeMoney.UnitTests;

public sealed class CartPostgresFactAttribute : FactAttribute
{
    public CartPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1")
            Skip = "اجرای آزمون مستقل PostgreSQL با BYEMONEY_CART_PG_TESTS=1 فعال می‌شود.";
    }
}
