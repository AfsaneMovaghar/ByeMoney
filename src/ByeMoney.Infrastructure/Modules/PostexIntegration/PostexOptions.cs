namespace ByeMoney.Infrastructure.Modules.PostexIntegration;

/// <summary>
/// تنظیمات اتصال و پیش‌فرض‌های عملیاتی پستکس.
/// سیاست مهلت پشتیبان تحویل در <c>PhysicalDelivery</c> است؛ اینجا فقط اتصال و ساخت مرسوله است.
/// </summary>
public sealed class PostexOptions
{
    public const string SectionName = "Postex";

    public const string DefaultOriginId = "ramsar";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.postex.ir/";

    /// <summary>کلید دسترسی پستکس؛ از طریق متغیر محیطی <c>POSTEX_API_KEY</c> هم قابل override است.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>دیگر در ساخت مرسوله خوانده نمی‌شود؛ فقط <c>pick_up</c> همان درخواست <c>ready_to_accept</c> می‌فرستد.</summary>
    public bool ReadyToAccept { get; set; }

    /// <summary>
    /// پروفایل‌های مبدأ/فرستنده با شناسهٔ پایدار مشترک با استرپی (مثل <c>ramsar</c>).
    /// </summary>
    public Dictionary<string, PostexSenderOptions> Origins { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// سازگاری عقب‌رو: اگر <see cref="Origins"/> خالی باشد، همین پروفایل به‌عنوان <c>ramsar</c> استفاده می‌شود.
    /// </summary>
    public PostexSenderOptions? Sender { get; set; }

    public PostexSenderOptions? ResolveOrigin(string? originId)
    {
        EnsureLegacySenderMapped();
        if (string.IsNullOrWhiteSpace(originId))
        {
            return null;
        }

        return Origins.TryGetValue(originId.Trim(), out var profile) ? profile : null;
    }

    public void EnsureLegacySenderMapped()
    {
        Origins ??= new Dictionary<string, PostexSenderOptions>(StringComparer.OrdinalIgnoreCase);
        if (Origins.Count > 0 || Sender is null)
        {
            return;
        }

        if (Sender.CityId > 0)
        {
            Origins[DefaultOriginId] = Sender;
        }
    }
}

/// <summary>پروفایل فرستنده/مبدأ برای یک انبار؛ city_id پستکس باید با مبدأ استرپی یکی باشد.</summary>
public sealed class PostexSenderOptions
{
    public string FirstName { get; set; } = "خاک";

    public string LastName { get; set; } = "تا افلاک";

    public string MobileNo { get; set; } = "09120000000";

    public string TelephoneNo { get; set; } = "02100000000";

    public string? CompanyName { get; set; } = "شرکت خاک تا افلاک";

    public string? NationalCode { get; set; }

    public string Address { get; set; } = "رامسر";

    /// <summary>کد شهر پستکس مبدأ.</summary>
    public int CityId { get; set; } = 573;

    public string CityName { get; set; } = "رامسر";

    public string? PostCode { get; set; }

    /// <summary>عرض جغرافیایی مبدأ؛ برای collection_type=pick_up اجباری است.</summary>
    public double? Lat { get; set; }

    /// <summary>طول جغرافیایی مبدأ؛ برای collection_type=pick_up اجباری است.</summary>
    public double? Lon { get; set; }

    /// <summary>
    /// فقط مرجع دستی آزمون مسیر مبدأ←مقصد (پیش‌فرض کد؛ در appsettings نباشد).
    /// مقصد واقعی ساخت مرسوله از آدرس سبد می‌آید و این فیلد خوانده نمی‌شود.
    /// </summary>
    public int TestDestinationCityId { get; set; } = 259;

    /// <summary>همراه <see cref="TestDestinationCityId"/>؛ فقط مرجع آزمون دستی.</summary>
    public string TestDestinationCityName { get; set; } = "مشهد";
}
