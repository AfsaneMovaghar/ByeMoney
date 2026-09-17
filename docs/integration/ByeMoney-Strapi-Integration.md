# ByeMoney ↔ TarhElahi — قرارداد اتصال و وضعیت پیاده‌سازی

نسخه ۲ — ۲۰۲۶-۰۹-۱۵ | مرجع فعال مرز سیستم‌ها، هویت خارجی، DTO و تحویل خرید

## I00 — راهنمای مراجعه ایجنت

قواعد کسب‌وکار فقط در [ByeMoney-Decisions.md](ByeMoney-Decisions.md) هستند. برای تسک اتصال، I01 و بخش مرتبط کافی است؛ برای تعرفه، تخفیف، لغو یا محدوده فاز ابتدا بخش متناظر Decisions خوانده شود. الزام تجاری جدید در این سند ساخته نشود.

| کار | بخش لازم |
|---|---|
| شناخت مرجع داده و محل کد | I01 و I13 |
| ورود، نگاشت و مجوز | I02–I03 |
| خواندن کاربر/دوره/فصل/محصول | I04–I06 |
| شارژ و رسید | I07 |
| خرید و اعلان نتیجه | I08–I09 |
| دسترسی، موجودی و ارسال | I10 |
| گزارش، Legacy و انتقال | I11–I12 |
| موارد ناقص و پذیرش | I14–I15 |
| تفاوت با سند قبلی | I16 |

برچسب‌ها: **موجود در کد**، **قرارداد هدف/نیازمند تکمیل**، **پیشنهاد طراحی** و **باز**. نبود برچسب آزمون به معنی اثبات اجرای موفق نیست. endpoint برنامه‌ریزی‌شده را در فرانت به‌عنوان سرویس آماده مصرف نکنید.

## I01 — مرز مسئولیت

| سیستم | مسئولیت اتصال |
|---|---|
| TarhElahi/Strapi | هویت خارجی، کاتالوگ و قیمت منبع، انتشار/فروش‌پذیری، اطلاعات ارسال/موجودی فعلی، اعطای دسترسی آموزشی، Legacy |
| ByeMoney | UserId داخلی/RBAC، نرخ تبدیل، TopUp، Wallet/Ledger، ثبت مالی سفارش و Snapshot، گزارش مالی جدید/تجمیعی |
| Next.js | رابط کاربر/ادمین و واسط کنترل‌شده؛ قیمت یا وضعیت ارسالی مرورگر مرجع مالی نیست |

ارتباط فقط API/Webhook/Event؛ دسترسی مستقیم به دیتابیس مقابل ممنوع. Domain بای‌مانی نباید ساختار رابطه‌های Strapi را بشناسد. کاتالوگ خود طرح الهی در Strapi می‌ماند؛ نسخه سفارش برای تحویل/نمایش در صورت نیاز projection است، نه حقیقت مالی دوم.

نور در Strapi نه محاسبه و نه به‌عنوان مانده مستقل ذخیره/تغییر شود. نمایش موجودی از بای‌مانی مجاز است. مقدار قدیمی light مرجع یا ورودی مهاجرت مانده نیست (D06).

## I02 — Identity و JWT

مرجع ثبت‌نام/Login فاز اول Strapi است. نگاشت:

- ExternalUserId = user.documentId.
- UserId داخلی بای‌مانی کلید همه ماژول‌های دامنه است.
- phone/email/username صفات کاربرند؛ شناسه دائمی اتصال نیستند.
- نبود documentId در JWT معتبر، Unauthorized صریح؛ fallback با id عددی/تلفن نداریم.
- اعتبار امضا، عمر توکن و سایر الزامات اعتبارسنجی پیکربندی‌شده قبل از مصرف claim بررسی شوند.
- فعالیت در زمان sync: IsActive = confirmed && !blocked؛ isMobileVerified داده مستقل است و خودکار شرط تازه فعالیت نشود.
- نقش JWT استرپی مجوز مالی بای‌مانی نیست؛ RBAC از بای‌مانی خوانده می‌شود.

تأیید قبلی پر بودن documentId در دیتابیس در سند قدیمی گزارش شده؛ در این کار دوباره DB بررسی نشده است. ادعای lookup دقیق «documentId» با قاعده رد نحوی همه رشته‌های عددی یکی نیست؛ تست رفتار با هویت‌های معتبر/نامعتبر لازم است.

سیاست تازگی blocked/confirmed، Force Logout، Revoke و احتمال TokenVersion باز است (I14). sync پس‌زمینه ناموفق نباید کاربر بدون نگاشت معتبر را وارد پرداخت کند.

## I03 — احراز هویت سرویس

مسیرهای اختصاصی Strapi از namespace زیر استفاده می‌کنند:

`/api/integrations/byemoney/v1/`

کلید هدر `X-Service-Key` با env `BYEMONEY_SERVICE_KEY` مقایسه می‌شود. policy: `src/policies/is-service-authenticated.js`؛ route با `auth:false` و `global::is-service-authenticated` محافظت می‌شود. auth:false به معنی عمومی‌بودن بدون policy نیست.

قرارداد معتبر: کلید حداقل ۳۲ بایت تصادفی امن؛ نگهداری در secret/env، نه repo یا log؛ خطای missing/invalid برابر 401. کد مشاهده‌شده از SHA-256 و crypto.timingSafeEqual استفاده می‌کند. middleware احراز هویت جهانی تکراری لازم نیست. چرخش فعلی دستی env است؛ zero-downtime ادعا نشده.

JWT کاربر برای APIهای کاربری بای‌مانی و کلید سرویس برای APIهای Integration دو مرز جدا هستند؛ کلید سرویس به مرورگر داده نشود.

## I04 — APIهای خواندن موجود

| مسیر Strapi | وضعیت / نتیجه |
|---|---|
| GET /api/integrations/byemoney/v1/users/:externalUserId | موجود؛ lookup فقط documentId؛ نبود کاربر 404 |
| GET /api/integrations/byemoney/v1/courses/:externalId | موجود؛ lookup documentId؛ نبود دوره 404 |

### I04-U — User DTO

```json
{
  "externalUserId": "user-document-id",
  "phoneNumber": null,
  "email": null,
  "firstName": null,
  "lastName": null,
  "confirmed": true,
  "blocked": false,
  "isMobileVerified": true,
  "createdAt": "2026-09-14T00:00:00Z",
  "updatedAt": "2026-09-14T00:00:00Z"
}
```

نمونه شکل قرارداد است؛ مقادیر واقعی نیستند. پاسخ whitelist-only؛ password، resetPasswordToken، confirmationToken، otpCode، otpExpiresAt، cartData و username برگردانده نشوند. از DTO صریح به مدل داخلی نگاشت شود؛ propertyهای قدیمی DisplayName/Mobile الزام نام‌گذاری نیستند.

### I04-C — Course DTO

```json
{
  "source": "tarh_elahi",
  "type": "course",
  "externalId": "course-document-id",
  "parentExternalId": null,
  "title": "عنوان دوره",
  "slug": "course-slug",
  "priceRial": 2500000,
  "published": true,
  "available": true,
  "updatedAt": "2026-09-14T00:00:00Z"
}
```

source/type قرارداد فعلی‌اند؛ slug برای مسیر نمایش است، نه هویت مالی. published از publishedAt و available فعلاً برابر published است؛ با افزودن قاعده واقعی فروش‌پذیری، معنای available حفظ و محاسبه داخلی اصلاح شود.

**فاصله کد و قرارداد:** getCourse فعلی Number(course.price) را با نام priceRial می‌فرستد و تخفیف را اعمال نمی‌کند. UI قیمت را تومان نمایش می‌دهد. معتبر بودن «واحد ریال» از نام فیلد نتیجه نمی‌شود و باید پیش از خرید واقعی رفع شود (I05).

## I05 — قیمت، نرخ، تخفیف و ارسال

قواعد تجاری در D06–D08 مرجع‌اند. مسئول تبدیل نهایی نور ByeMoney است. تعریف صریح قرارداد: نرخ = تعداد ریال به ازای یک نور؛ Noor = FinalRial / RialPerNoor. اگر قیمت منبع تومان است، ابتدا Rial = Toman × 10. نمایش نرخ مستقل و ثابت در Next.js حذف شود.

priceRial باید decimal-compatible باشد؛ تبدیل اجباری به int/long یا گردکردن ضمنی در JavaScript مرجع نشود. مدل مالی .NET از decimal استفاده کند؛ دقت ذخیره Wallet مشاهده‌شده ۴ اعشار است، اما این به‌تنهایی سیاست گردکردن تجاری نیست (Q02). نحوه serialization و جلوگیری از از‌دست‌رفتن دقت باید در DTO نهایی تست شود.

قیمت نهایی سرور باید قیمت پایه، تخفیف زمان‌دار معتبر، کوپن معتبر، تعداد، ارسال و مبلغ نهایی را پوشش دهد. تخفیف ۱۰۰٪ جریان رایگان دارد؛ Validator فعلی دوره صفرمبلغ را رد می‌کند و باید اصلاح شود. شارژ نور کوپن خرید نمی‌گیرد.

**مرز اجرای تخفیف هنوز طراحی لازم دارد:** داده و محاسبه فعلی تخفیف در Strapi است؛ یا سرویس قیمت معتبر آن برای بای‌مانی قرارداد می‌شود، یا محاسبه مالی به بای‌مانی منتقل و داده لازم از Strapi خوانده می‌شود. یکی انتخاب و مصرف کوپن با سفارش هماهنگ شود؛ دو مرجع مستقل قیمت/مصرف ساخته نشود. حفظ رفتار فعلی ترکیب تخفیف‌ها تصویب شده، نه اعتماد به item.price مرورگر.

هزینه ارسال و کاهش موجودی طبق کاربر موجودند. فرمول، مناطق، آدرس و اثر تخفیف بر ارسال از پیاده‌سازی/تنظیمات واقعی استخراج شود؛ صفر بودن پیش‌فرض فرض نشود.

## I06 — هویت فصل و Product

دوره: ExternalId = course.documentId. Product باید شناسه پایدار داشته باشد؛ استفاده از product.documentId پیشنهاد هماهنگ با Strapi است و باید وجود/پایداری در قرارداد و داده تأیید شود.

فصل مستقل: id عددی component، جایگاه آرایه، ترتیب نمایش یا slug شناسه مالی نیستند. پیشنهاد قبلی `integrationId: UUID` حفظ می‌شود؛ ایجاد، backfill و حفظ آن در ویرایش/انتشار لازم است. این ستون به‌عنوان پیاده‌شده ادعا نمی‌شود.

قرارداد هدف فصل: type=course_chapter، externalId=شناسه پایدار فصل، parentExternalId=course.documentId. خواندن فصل/محصول و موجودی در مسیرهای Integration مشاهده‌شده وجود نداشت؛ URL و DTO نهایی پیش از استفاده تعریف شوند.

شناسه قدیمی chapterId در enrolledChapters برای داده داخلی Strapi می‌تواند با نگاشت نگهداری شود؛ بای‌مانی نباید به همان عدد وابسته شود.

## I07 — شارژ کارت‌به‌کارت

### مسیرهای موجود در بای‌مانی

| مسیر | کاربرد |
|---|---|
| POST /api/topup/requests | ایجاد درخواست کاربر احرازشده |
| GET /api/admin/topups/by-reference/:clientReferenceCode | جست‌وجوی درخواست توسط ادمین مجاز |
| POST /api/admin/topups/{id}/confirm | تأیید با ExternalTransactionId و ConfirmedAmount |
| POST /api/admin/topups/{id}/reject | رد با علت |

کنترلر ادمین RequireTopUpReview دارد. وجود این مجوز به معنی تکمیل مجوز تزریق یا خروج نیست. خروجی درخواست شامل شناسه TopUp و کد پیگیری است؛ نام ClientReferenceId/Code بین DTOها پیش از اتصال دقیق هماهنگ شود.

### قرارداد هدف تکمیلی

درخواست پایدار باید user، مبلغ ریالی، مقدار نور، نرخ زمان درخواست، مهلت، روش پرداخت، رسید/مدرک، کد پیگیری، وضعیت و شناسه پرداخت خارجی داشته باشد. مبلغ از متن notes یا عنوان محصول استخراج نشود. Amount فعلی در Confirm مستقیماً به Wallet افزوده می‌شود؛ نباید همان عدد ریالی اشتباهاً به‌عنوان نور ارسال شود.

Pending → Confirmed یا Rejected در مدل پایه موجود است. انقضا، بارگذاری/اصلاح رسید و رسید دیرهنگام باید با Q01 طراحی شوند؛ این حالت‌ها امروز موجود فرض نشوند. تأیید فقط بعد از تطبیق واقعی پرداخت/مبلغ انجام شود. پرداخت دستی بانک بیرون سیستم می‌ماند، مرجع ثبت نور بای‌مانی است.

تکرار ترتیبی تأیید با همان ExternalTransactionId در کد no-op است؛ تضمین هم‌زمانی/یکتایی بین درخواست‌ها نیاز آزمون و قید پایگاه دارد. درخواست تکراری create نیز نباید چند شارژ برای یک پرداخت بسازد. رفتار شناسه ناسازگار خطای صریح باشد.

نگهداری فایل رسید می‌تواند از زیرساخت موجود استفاده کند؛ شناسه/مالکیت، دسترسی و ارتباط آن با TopUp قطعی باشد. اعطای نور توسط endpoint عمومی payment-light و افزایش light استرپی حذف/مسدود شود.

## I08 — خرید، Snapshot و اعلام نتیجه

ترتیب هدف:

۱. هویت معتبر و انتخاب قلم/تعداد. ۲. خواندن قیمت/مالکیت/موجودی معتبر. ۳. محاسبه مبلغ نهایی و نور. ۴. ثبت اتمیک خرید، Ledger و داده پایدار اعلان. ۵. ارسال نتیجه به Strapi. ۶. ثبت وضعیت تحویل و پاسخ قابل پیگیری.

در فلو دوره، قبل از پرداخت یک normal pending order در Strapi ایجاد نشود. این منع مربوط به سفارش فروش نهایی است؛ TopUp و پیش‌فاکتور/رزرو کنترل‌شده کالا مفاهیم جدا هستند. رزرو فیزیکی در I10 تعریف شود تا lifecycle قدیمی پیش از موعد موجودی را کم نکند.

Snapshot هدف: ProductSource، ExternalItemId، ExternalParentId، ItemType، Title، SellerId/Account، Quantity، قیمت پایه/نهایی ریالی، تخفیف/کوپن، ارسال، ConversionRate، PriceNoor، PurchasedAt. این‌ها مفاهیم لازم‌اند؛ مدل دقیق سبد و نام فیلدهای نسخه جدید هنوز باید توافق شود.

### endpoint هدف قبلی؛ هنوز نیازمند تکمیل

`POST /api/integrations/byemoney/v1/purchases/confirm`

سند قدیمی eventId و dealId داشت؛ DTO کد بای‌مانی بررسی‌شده PurchaseId می‌فرستد. پیش از اتصال باید نام wire و نگاشت یکسان شود؛ صرفاً یکی فرض نشود. پیشنهاد: یک شناسه خرید پایدار با alias سازگار برای قرارداد قدیمی.

payload مفهومی: eventId، شناسه خرید، buyerExternalUserId، item یا items، snapshot و purchasedAt. کد فعلی اعلان یک course دارد؛ سبد نیاز شناسه ردیف و وضعیت هر ردیف دارد. eventId برای retry همان رویداد ثابت بماند؛ شناسه خرید/ردیف نیز برای dedup کافی و پایدار تعریف شود.

همان شناسه با همان محتوا پس از تکمیل → success/no-op؛ همان شناسه با محتوای متناقض → conflict. درخواست تکراری خرید در بای‌مانی نیز جداگانه کنترل شود؛ dedup گیرنده جای جلوگیری از debit دوباره را نمی‌گیرد.

## I09 — تحویل قابل بازیابی و Outbox

تعهد طراحی معتبر قبلی: ثبت مالی و داده پایدار ارسال نتیجه در یک تراکنش محلی؛ ارسال بعد از commit. implementation می‌تواند Outbox صریح یا رکورد پایدار خرید قابل retry باشد، مشروط به حفظ همین تضمین. نام یک جدول به‌تنهایی صحت تحویل را ثابت نمی‌کند.

وضعیت مالی موفق + تحویل Pending مجاز است. خطای موقت Strapi دلیل rollback خودکار مالی نیست. اعلان با retry و ثبت تلاش/خطا پیگیری شود؛ خطای دائمی صف رسیدگی داشته باشد. API موفق تحویل فقط پس از ثبت پایدار عملیات مقصد پاسخ دهد.

GET هدف پیشین برای تطبیق:

`GET /api/integrations/byemoney/v1/purchases/:dealId`

هنوز برنامه‌ریزی‌شده؛ نام پارامتر با PurchaseId نهایی هماهنگ شود. وضعیت دسترسی/تحویل و شناسه خرید برای تطبیق لازم‌اند. شناسه خرید و CorrelationId در log هر دو سیستم ثبت شوند، نه کلید/توکن.

**محدودیت استقلال:** Outbox وابستگی بعد commit را رفع می‌کند؛ پیش از پرداخت، قیمت معتبر کاتالوگ و موجودی همچنان وابسته به Strapi است. cache تأییدنشده یا قیمت مرورگر جایگزین خودکار نیست.

## I10 — Entitlement و کالای فیزیکی

بای‌مانی فرمان دامنه‌ای «اعطای دوره/فصل مشخص به کاربر مشخص» می‌فرستد؛ ساختار relations، enrolledChapters و idهای داخلی را Strapi مدیریت می‌کند. اعطای مجدد به مالک فعلی موفق/no-op باشد؛ ولی قبل از پرداخت جدید، مالکیت Legacy نیز چک شود تا هزینه بی‌دلیل دریافت نشود.

لغو عمومی دوره وجود ندارد (D09). شکست فعال‌سازی باید گزارش/بازیابی شود؛ صرف log و پاسخ موفق کافی نیست. مسیرهای فعلی فعال‌سازی در Next و lifecycle Strapi نباید دو اجرای ناسازگار تولید کنند؛ یک مسیر مرجع تحویل انتخاب شود.

کالای فیزیکی داخل فاز اول است. ثبت تعداد، آدرس، هزینه ارسال و وضعیت آماده‌سازی/ارسال/تحویل لازم است. پیشنهاد طراحی: عملیات رزرو/تثبیت/آزادسازی موجودی شناسه پایدار سفارش/ردیف داشته باشند؛ کاهش در رخداد مناسب و فقط یک بار انجام شود. انتخاب جزئیات با رفتار موجود و Q05 هماهنگ شود.

کد lifecycle دیده‌شده بعد create/update بدون شرط پرداخت stock را کم می‌کند؛ stockDeducted فقط گارد ساده است. جلوگیری از فروش بیش از موجودی، race و بازیابی نیمه‌کاره باید مستقل تضمین شود؛ Math.max(0, stock-quantity) تضمین کفایت موجودی نیست.

## I11 — پنل، خروج و گزارش

فهرست/فیلتر سفارش، رسید، سفارش دستی و تاریخچه با مرجع مالی جدید تطبیق یابند. اگر order projection در Strapi نگهداری شد، purchaseId و owner/source جریان داشته باشد؛ تغییر نمایشی paymentStatus نتواند نور ایجاد کند.

تگ تسویه/بستن دوره فقط گزارش است، بدون عملیات Ledger. خروج نور API/مجوز مستقل لازم دارد؛ URL آن در بررسی موجود اثبات نشده و این سند endpoint آماده جعل نمی‌کند. اجرای خروج تابع D09 و Q06، با علت/پیگیری و کنترل تکرار است.

Legacy فقط گزارش: `GET /api/integrations/byemoney/v1/legacy-sales` در سند قبلی برنامه‌ریزی شده بود؛ نیاز pagination، فیلتر تاریخ/update، شناسه پایدار سفارش و وضعیت دارد. این endpoint پیاده‌شده ادعا نمی‌شود. داده Legacy به Reporting View می‌رود، نه Ledger نور.

## I12 — انتقال و مسیرهای ممنوع

زمان دقیق Go-Live به‌علاوه نوع پرداخت و مالک جریان سفارش ثبت شود. سفارش کارت‌به‌کارت باز قبل انتشار مطابق سیاست Q07 تکمیل شود؛ از تبدیل خودکار صرفاً بر اساس تاریخ پرهیز شود. دسترسی قبلی کاربران حفظ شود.

مسیرهای ممنوع: دسترسی DB مستقیم، تغییر light مستقل، قیمت معتبر از مرورگر، تایید پرداخت بانکی شبیه‌سازی‌شده، slug/phone/id عددی به‌عنوان شناسه دائمی خارجی، تبدیل Legacy به Ledger نور، اعطای تحویل تکراری یا کسر دوباره در retry.

قاعده قدیمی «Move card-to-card payment into ByeMoney ممنوع» دیگر معتبر نیست. درخواست/تأیید مالی و Ledger به بای‌مانی منتقل می‌شوند؛ عملیات دستی بانک و ذخیره رسانه رسید می‌توانند خارج هسته باشند. مالک اجرای درگاه آتی هنوز جدا تصمیم می‌خواهد.

## I13 — وضعیت مشاهده‌شده کد و منابع

تاریخ بررسی کد مبنا ۲۰۲۶-۰۹-۱۴ است؛ در ۱۵ سپتامبر متن دو سند مرجع دوباره خوانده شد، نه تمام کد. هیچ build، تست یکپارچه یا تراکنش پروداکشن توسط این بررسی اجرا نشده است. عبارت «implemented and tested» سند قدیمی گزارش تاریخی تیم است، نه نتیجه تازه این کار.

| مخزن/برنچ | شناسه ثبت‌شده |
|---|---|
| FrontendArtist/front-end / master | commit 5c35c178c5a7b9cc75078928075b53522f3ef494 |
| FrontendArtist/front-end / ByeMoney | commit f83b898d19d91c46bef0e0c8a8d9e28b71164c4c |
| FrontendArtist/tarhelahi-backend / byeMoney | tree 38c5ebdff5cfb4e7a3c3a88e0dadb53d44ca5058؛ شناسه درخت، نه commit |
| AfsaneMovaghar/ByeMoney / feature/course-purchase-slice | SHA کامیت بررسی قبلی ثبت نشده؛ محدودیت تطبیق دقیق |

master طبق اعلام کاربر production است؛ استقرار مستقلاً تأیید نشده. مخزن قدیمی AfsaneMovaghar/tarhelahi-backend مبنای بک نهایی نیست. برنچ فرانت ByeMoney نسبت به master در بررسی ۳ کامیت جلو و ۲ عقب بود؛ تغییرات sync به معنی اتصال خرید نیست. لینک‌های برنچی زیر ممکن است تغییر کنند.

| شاهد | مشاهده و اقدام |
|---|---|
| [Strapi routes](https://github.com/FrontendArtist/tarhelahi-backend/blob/byeMoney/src/api/integration/routes/integration.js) | فقط GET کاربر/دوره؛ confirm/status/legacy و فصل/محصول در این routes نبود |
| [Strapi controller](https://github.com/FrontendArtist/tarhelahi-backend/blob/byeMoney/src/api/integration/controllers/integration.js) | documentId و DTO محدود؛ قیمت بدون تخفیف/تبدیل واحد صریح |
| [policy](https://github.com/FrontendArtist/tarhelahi-backend/blob/byeMoney/src/policies/is-service-authenticated.js) | کلید سرویس و timingSafeEqual موجود |
| [coupon](https://github.com/FrontendArtist/tarhelahi-backend/blob/byeMoney/src/api/coupon/services/coupon.js) | محاسبه از item.price، تخفیف احتمالی شارژ، شمارنده read+update؛ نیاز اصلاح و آزمون رقابت |
| [order lifecycle](https://github.com/FrontendArtist/tarhelahi-backend/blob/byeMoney/src/api/order/content-types/order/lifecycles.js) | دسترسی/موجودی/اکسل؛ خطا فقط log، برگشت موجودی در همین فایل مشاهده نشد |
| [Next orders](https://github.com/FrontendArtist/front-end/blob/master/src/app/api/orders/route.js) | دوره/فصل/محصول/شارژ، کوپن و free؛ بعضی وضعیت‌ها و قیمت از request پذیرفته می‌شوند |
| [Next light](https://github.com/FrontendArtist/front-end/blob/master/src/app/api/payment-light/route.js) | افزایش مستقیم light بدون اثبات پرداخت در این handler؛ جایگزین شود |
| [Next admin order](https://github.com/FrontendArtist/front-end/blob/master/src/app/api/admin/orders/%5Bid%5D/route.js) | مقدار نور از notes و امکان شارژ تکراری؛ جایگزین شود |
| [Next manual](https://github.com/FrontendArtist/front-end/blob/master/src/app/api/admin/orders/manual/route.js) | کاربر موجود/جدید و اعطای دوره/فصل؛ در مهاجرت حفظ شود |
| [Next verify](https://github.com/FrontendArtist/front-end/blob/master/src/app/api/payment/verify/route.js) | Verify بانکی تکمیل نشده؛ شبیه‌سازی مسدود شود |
| [Next constants](https://github.com/FrontendArtist/front-end/blob/master/src/lib/constants.js) | نرخ ثابت ۱۰۰۰ تومان؛ با Settings جایگزین شود |
| [Next sync](https://github.com/FrontendArtist/front-end/blob/ByeMoney/src/lib/byeMoneySync.js) | POST /api/auth/sync با JWT و retry یک‌باره؛ خرید/کیف پول نیست |
| [Purchase handler](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Application/Modules/Purchases/Commands/PurchaseCourse/PurchaseCourseCommandHandler.cs) | خرید تک‌دوره، نرخ/Snapshot، debit و credit سیستم، Save قبل اعلان |
| [Purchase validator](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Application/Modules/Purchases/Commands/PurchaseCourse/PurchaseCourseCommandValidator.cs) | فعالیت/انتشار/مالکیت/موجودی؛ قیمت صفر رد می‌شود |
| [TopUp confirm](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Application/Modules/Wallet/Commands/ConfirmTopUp/ConfirmTopUpCommandHandler.cs) | دو Ledger، کنترل تکرار ترتیبی؛ هم‌زمانی آزموده نیست |
| [TopUp create](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Application/Modules/Wallet/Commands/CreateTopUpRequest/CreateTopUpRequestCommandHandler.cs) | Amount/روش/شناسه خارجی؛ Snapshot ریال/نرخ در این جریان نیست |
| [Notifier](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Application/Modules/Purchases/Services/CoursePurchaseNotifier.cs) | وضعیت اعلان/خطا؛ EventId در هر call جدید است، PurchaseId پایدار باید برای dedup لحاظ شود |
| [Settings](https://github.com/AfsaneMovaghar/ByeMoney/blob/feature/course-purchase-slice/src/ByeMoney.Infrastructure/Modules/Settings/Persistence/SystemSettingRepository.cs) | نرخ پویا با fallback؛ سیاست تنظیم نامعتبر روشن شود |

شواهد مکمل: درخت بای‌مانی unit test و retry background service دارد؛ اجرا نشده‌اند. در WalletConfiguration خوانده‌شده دقت ۴ اعشار و index یکتای User/Account هست؛ concurrency token دیده نشد. در CoursePurchaseConfiguration خوانده‌شده index خریدار/وضعیت هست؛ قید یکتایی خرید کاربر/قلم مشاهده نشد. این مشاهده محدود جای بررسی همه کنترل‌های repository/transaction را نمی‌گیرد.

برای ادامه: SHA جدید را ثبت و diff فایل‌های مرتبط را بخوانید؛ موارد متاثر این جدول و تست مرتبط به‌روز شوند. برای بای‌مانی که SHA قبلی نداریم، تطبیق فایل‌های کلیدی لازم است و مقایسه دقیق commit قبلی قابل ادعا نیست.

## I14 — طراحی‌های باز اتصال

| ID | خروجی لازم | زمان |
|---|---|---|
| IC01 | واحد واقعی price، serialization decimal، سیاست گردکردن و نرخ fallback | پیش از قیمت نهایی؛ مرتبط Q02 |
| IC02 | integrationId فصل، نگاشت داده قدیمی و Product DTO | پیش از خرید فصل/کالا |
| IC03 | مالک موتور تخفیف، مصرف/رزرو کوپن، سقف رقابتی و سفارش شکست‌خورده | پیش از تخفیف |
| IC04 | یکسان‌سازی dealId/PurchaseId، EventId پایدار، سبد و شناسه ردیف | پیش از confirm |
| IC05 | idempotency ایجاد شارژ/خرید و کنترل رقابت DB | پیش از عرضه |
| IC06 | حفظ اتمیک داده ارسال، retry، reconciliation و رسیدگی دائمی | پیش از تحویل |
| IC07 | freshness وضعیت کاربر، revoke و TokenVersion احتمالی | پیش از اعتبارسنجی نهایی هویت |
| IC08 | سفارش projection، رزرو/موجودی و API وضعیت ارسال/لغو | پیش از فیزیکی |
| IC09 | قرارداد لیست رسید/گردش/خروج، خطاها و مجوزها | پیش از پنل |
| IC10 | schema Legacy، pagination/update، زمان و مالک سفارش باز | پیش از انتقال |
| IC11 | مدل PaymentTransaction و Verify درگاه آینده | هنگام فاز درگاه |

این‌ها کار طراحی تیم‌اند؛ اصل قابلیت‌های پاسخ‌داده‌شده در Decisions دوباره باز نشود. نام endpoint پیشنهادی تا پیاده‌سازی/آزمون برچسب «هدف» حفظ کند.

## I15 — پذیرش اتصال و انتشار

- [ ] JWT نامعتبر/فاقد documentId و کلید سرویس نامعتبر رد شوند؛ DTO هیچ secret برنگرداند.
- [ ] واحد ریال/تومان و نور از ابتدا تا Ledger یکسان؛ قیمت مرورگر دستکاری‌شده بی‌اثر باشد.
- [ ] شارژ با تأیید ترتیبی/هم‌زمان فقط یک اثر؛ رد هیچ اثر مالی؛ تغییر نرخ Snapshot را عوض نکند.
- [ ] خرید و اعلام نتیجه تکراری فقط یک debit و یک اعطای دسترسی ایجاد کنند؛ payload متناقض خطا دهد.
- [ ] قطع Strapi پس از commit قابل بازیابی باشد؛ قیمت نامعتبر پیش از commit مصرف نشود.
- [ ] رایگان، تخفیف ۱۰۰٪، ترکیب تخفیف، سقف کوپن، فصل و مالکیت قدیمی آزموده شوند.
- [ ] تعداد و هزینه ارسال درست؛ آخرین موجودی هم‌زمان بیش‌فروش نشود؛ لغو مجاز فیزیکی هماهنگ برگشت دهد.
- [ ] کاربر فاقد مجوز خروج/تأیید/رسید دیگران را نتواند انجام/مشاهده کند؛ تگ تسویه مالی نباشد.
- [ ] light و API شبیه‌سازی نتوانند حقیقت مالی بسازند؛ Legacy Ledger نور تولید نکند.
- [ ] نسخه سه پروژه، تنظیمات محیط، migration، پشتیبان، recovery و سفارش‌های باز ثبت شده باشند.

تمام موارد بالا در این بازنویسی اجرا نشده‌اند. نتیجه واقعی، محیط و کامیت پس از اجرا ثبت شوند؛ چک‌لیست قبلی 05-Acceptance-Release.md مکمل است و در تعارض این مرجع و Decisions مقدم‌اند.

## I16 — تغییرات نسبت به قرارداد قدیمی

محتوای کامل ۲۱ بخش نسخه قبلی بررسی شد. شناسه‌ها، User/Course DTO، کلید سرویس، فصل پایدار، Snapshot، Outbox، confirmation/status و Legacy حفظ و وضعیتشان روشن شد. اصلاحات: فیزیکی داخل دامنه؛ انتقال TopUp/تأیید کارت‌به‌کارت مجاز و لازم؛ واحد price نیاز راستی‌آزمایی؛ EventId/PurchaseId نیاز هماهنگی؛ ادعای تست تاریخی جدا از مشاهده فعلی؛ Go-Live با مالک جریان؛ دریافت نتیجه و خواندن قیمت دو وابستگی جدا هستند.

نسخه منبع از docs/integration/ByeMoney-Strapi-Integration.md در feature/course-purchase-slice خوانده شد؛ blob برابر 5cccba2b0e4d4db8d31a4e8fa33f51e19896ad06. قواعد تجاری جدید در D15 خلاصه شده‌اند. این سند جای نسخه قبلی هم‌نام است، نه تغییر خودکار APIهای اجراشده.
