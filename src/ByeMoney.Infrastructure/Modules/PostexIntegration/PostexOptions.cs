namespace ByeMoney.Infrastructure.Modules.PostexIntegration;

/// <summary>
/// تنظیمات اتصال و پیش‌فرض‌های عملیاتی پستکس.
/// سیاست مهلت پشتیبان تحویل در <c>PhysicalDelivery</c> است؛ اینجا فقط اتصال و ساخت مرسوله است.
/// </summary>
public sealed class PostexOptions
{
    public const string SectionName = "Postex";

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

    public PostexSenderOptions Sender { get; set; } = new();
}

/// <summary>فرستندهٔ ثابت شرکت؛ مبدأ پیش‌فرض رامسر (city_id=573).</summary>
public sealed class PostexSenderOptions
{
    public string FirstName { get; set; } = "خاک";

    public string LastName { get; set; } = "تا افلاک";

    public string MobileNo { get; set; } = "09120000000";

    public string TelephoneNo { get; set; } = "02100000000";

    public string? CompanyName { get; set; } = "شرکت خاک تا افلاک";

    public string? NationalCode { get; set; }

    public string Address { get; set; } = "رامسر";

    /// <summary>کد شهر پستکس برای رامسر.</summary>
    public int CityId { get; set; } = 573;

    public string CityName { get; set; } = "رامسر";

    public string? PostCode { get; set; }

    /// <summary>فقط مرجع آزمون مسیر رامسر←مشهد؛ مقصد واقعی از آدرس سبد می‌آید.</summary>
    public int TestDestinationCityId { get; set; } = 259;

    public string TestDestinationCityName { get; set; } = "مشهد";
}
