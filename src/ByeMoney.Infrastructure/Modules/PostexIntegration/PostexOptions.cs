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

    /// <summary>نوع جمع‌آوری: <c>pick_up</c> یا <c>postex_drop_off</c>.</summary>
    public string CollectionType { get; set; } = "pick_up";

    public string DefaultCourier { get; set; } = "IR_POST";

    public string DefaultServiceType { get; set; } = "EXPRESS";

    /// <summary>شناسهٔ جعبهٔ استاندارد پستکس؛ پیش‌فرض جعبه سایز ۲.</summary>
    public int DefaultBoxTypeId { get; set; } = 5;

    public int DefaultWeightGrams { get; set; } = 250;

    /// <summary>ارزش اظهارشدهٔ بیمه به ریال وقتی نرخ تبدیل نور در دسترس نیست.</summary>
    public long DefaultParcelValueIrr { get; set; } = 50_000_000;

    /// <summary>نرخ ریال به‌ازای نور فقط برای ارزش اظهارشدهٔ مرسوله؛ در مبلغ سبد اثر ندارد.</summary>
    public long RialPerNoorForDeclaredValue { get; set; } = 10_000;

    /// <summary>اگر false باشد مرسوله بدون اعلام آمادگی جمع‌آوری ثبت می‌شود.</summary>
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

    /// <summary>فقط مرجع آزمون مسیر مبدأ←مقصد؛ مقصد واقعی از آدرس سبد می‌آید.</summary>
    public int TestDestinationCityId { get; set; } = 259;

    public string TestDestinationCityName { get; set; } = "مشهد";
}
