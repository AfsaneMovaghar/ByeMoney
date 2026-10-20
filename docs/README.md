# راهنمای مشترک سه پروژه

این نقطهٔ ورود مستندات بای‌مانی، فرانت‌اند طرح الهی و بک‌اند Strapi است. بازبینی: ۲۰۲۶-۱۰-۲۰. لینک‌های بین مخزن‌ها بر اساس چیدمان فعلی `ByeMoney` و `khakTaAflak/front-end` و `khakTaAflak/backend` هستند.

## مرجع هر موضوع

| موضوع | مرجع اصلی |
|---|---|
| قواعد کسب‌وکار، مالکیت و معماری | [تصمیم‌ها](architecture/ByeMoney-Decisions.md)؛ نقشهٔ راه در D11 و معیار عرضه در D12 |
| API، DTO، هویت و رفتار اتصال | [قرارداد اتصال](integration/ByeMoney-Strapi-Integration.md) |
| مجوزهای بای‌مانی | [ماتریس دسترسی](architecture/Permissions-Matrix.md) |
| کارهای باز بای‌مانی و اتصال | [TASKS](../TASKS.md)؛ دامنه، وابستگی و معیار پذیرش هر کار |
| باگ‌های مستقل سایت فعلی | [HOTFIXES](../HOTFIXES.md)؛ مسئول پیگیری محمد |
| روش آزمون و انتشار | [پذیرش و انتشار](testing/05-Acceptance-Release.md)، شامل چک‌لیست SEP |
| نتیجهٔ آزمون و محدودیت پذیرش | [خلاصهٔ شواهد](testing/Verification-Summary.md) |
| اجرا و شناخت کد فرانت | [README فرانت](../../khakTaAflak/front-end/README.md) و [راهنمای فنی](../../khakTaAflak/front-end/docs/Technical-Guide.md) |
| اجرا و شناخت کد Strapi | [README بک](../../khakTaAflak/backend/README.md) |
| راه‌اندازی شبیه‌ساز درگاه | [SEP محلی فرانت](../../khakTaAflak/front-end/docs/testing/sep-local-mock.md) |
| دستور عامل و بازبینی کد | [AGENTS](../AGENTS.md) و [پرامپت بازبینی](prompts/code-review-prompt.md) |

[تحلیل اولیه و بازنگری](<../../مستندات بای مانی/ByeMoney-Analysis-Review.md>) منبع بیرون از این سه مخزن است؛ تصمیم جاری از D01 خوانده شود. نیاز خرید انتخابی مجموعه در D18 حفظ شده و تا تصمیم مالک، تسک اجرایی نیست.

## اجرای محلی بای‌مانی

پیش‌نیاز کد فعلی .NET 10 و PostgreSQL است. تنظیمات اتصال، JWT و کلیدهای سرویس را برای محیط توسعه در secret/env آماده کنید؛ قرارداد هویت و کلیدها در I02–I03 است. مقدار محرمانه در سند یا Git قرار نگیرد.

از ریشهٔ بای‌مانی:

```powershell
dotnet restore ByeMoney.sln
dotnet build ByeMoney.sln
dotnet run --project src/ByeMoney.API -- --environment Development
```

فرمان‌های آزمون:

```powershell
dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj
$env:BYEMONEY_CART_PG_TESTS = '1'
dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj
```

آزمون‌های سبد/تاریخچهٔ PostgreSQL با این متغیر فعال می‌شوند و از اتصال توسعه برای schemaهای موقت استفاده می‌کنند؛ اتصال باید مختص محیط آزمون باشد. آزمون‌های مستقل Wallet به `BYEMONEY_TEST_CONNECTION_STRING` نیاز دارند. Skip یا نبود اتصال، قبولی آن آزمون‌ها نیست. این فرمان‌ها در پاک‌سازی مستندات اجرا نشده‌اند.

## نگهداری اسناد

- هر موضوع یک مرجع دارد؛ برای شاخه یا چت فهرست تسک، قرارداد یا گزارش موازی نسازید. تغییر سند همراه تغییر مرتبط ادغام شود.
- پس از احراز تکمیل، کار انجام‌شده از TASKS/HOTFIXES حذف شود. اگر فقط پذیرش یا انتشار مانده، همان بخش باقی بماند.
- شاهد آزمون به تاریخ، نسخه، محیط، نتیجه و محدودیت خلاصه شود؛ کد آماده، آزمون محلی و ادغام در master به معنی انتشار نیستند.
- قرارداد مطلوب و رفتار مشاهده‌شدهٔ کد جدا ثبت شوند. اختلاف آن‌ها با بازنویسی یک تصمیم مصوب پنهان نشود.
- تاریخچهٔ جزئی تغییرات در Git است. فهرست فایل‌های تغییرکرده، شرح تلاش ابزارها و پچ اعمال‌شده را به سند فعال تبدیل نکنید.
- اسناد و مجوزهای وابستگی‌ها، از جمله TinyMCE، متعلق به پروژه نیستند و در این سامان‌دهی حذف نمی‌شوند.
