namespace ByeMoney.UnitTests;

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING")))
            Skip = "برای آزمون دیتابیس، اتصال محلی BYEMONEY_TEST_CONNECTION_STRING لازم است.";
    }
}
