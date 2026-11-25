# ۰۲ — سبد و خرید

> دامنه: دوره، فصل، دورهٔ فصلی، کالا با تعداد، سبد مختلط، کوپن، سفارش رایگان، کسری موجودی، خرید تکراری. قالب: [README](README.md).
> معیارها از I09، I09-C، I09-CH و I09-PH در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، D08/D09/D18 در [تصمیم‌ها](../../architecture/ByeMoney-Decisions.md) و ردیف‌های R01، R06، «پذیرش سرتاسری فصل، کالا و سبد مختلط» و FE-ORDER-02 در [TASKS](../../../TASKS.md).
> وضعیت اجرا (۲۰۲۶-۱۱-۲۵): بخش‌های الف، ب، ج، د، ز و ط با دستهٔ `09120000027`–`…34` و بخش‌های `ه`، `و` و `ح` با دستهٔ `09120000057`–`…67` و پستکس زنده (با قیمت موقت `12,000` نور برای عبور از کف ارزش اعلامی پستکس، `rialPerNoorForShipping=10`) با HTTP واقعی اجرا و تطبیق شدند. سه نگاه مرورگر (`الف#۱۱`، `د#۵`، `و#۵`) در این اجراها انجام نشدند.

## پیش‌نیاز

- چهار سرویس بالا باشند (استرپی `:1337`، بای‌مانی `:5000`، فرانت `:3000`، PostgreSQL).
- `.\\seed.ps1 -Action up` **با ادمین استرپی** اجرا شده باشد؛ آدرس خریداران، کالای کم‌موجودی و کوپن تک‌نفره فقط در این حالت ساخته می‌شوند.
- `.\\seed.ps1 -Action buyers` **یک بار پیش از هر اجرای این سناریو** اجرا شود. خرید دوره و فصل برگشت‌ناپذیر است و `reset` فقط دیتابیس بای‌مانی را پاک می‌کند، نه مالکیت‌ها را در استرپی؛ پس هر اجرا دستهٔ تازهٔ خریداران می‌خواهد.
- `.\\seed.ps1 -Action verify` سبز باشد (شامل `usedCount=0` کوپن `MANUALTEST1` و «بدون خرید قبلی» هر خریدار).
- تنظیمات ارسال (`api::shipping-setting.shipping-setting`): `costMode=fixed`، `fixedAmountInNoor` پر، `rialPerNoorForShipping` پر. اگر `costMode=postex` است گام‌های کالا نیازمند تأیید فراخوانی پستکس هستند (بخش «خارج از دامنه»).
- موجودی P1 (کالای ۳۰ نوری) حداقل ۴ و موجودی P2 (کالای کم‌موجودی) دقیقاً ۱ باشد؛ هر دو را در Content-Manager بخوانید و یادداشت کنید.

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` بردارید، حدس نزنید):

| نماد | چیست | کلید در `fixtures` | مقدار ثابت |
|---|---|---|---|
| C1 | دورهٔ پایه، ۲۰ نور | `basicCourseDocumentId` | — |
| C2 | دورهٔ فصلی؛ قیمت پکیج ۰ | `seasonalCourseDocumentId` | — |
| CH1 / CH2 | فصل ۱ و ۲ دورهٔ فصلی، هرکدام ۱۰ نور | `seasonalChapter1IntegrationId` / `seasonalChapter2IntegrationId` | `a0000001-…c001` / `a0000002-…c002` |
| P1 | کالای فیزیکی، ۳۰ نور، موجودی ۱۰ | `physicalProductDocumentId` | — |
| P2 | کالای کم‌موجودی، ۵ نور، موجودی ۱ | `lowStockProductDocumentId` | — |
| K10 | کوپن ۱۰٪ روی همهٔ دوره‌ها، بدون سقف | `couponDocumentId` | کد `MANUALTEST10` |
| K1 | کوپن ۱۰٪ روی همهٔ دوره‌ها، سقف مصرف ۱ | `limitedCouponDocumentId` | کد `MANUALTEST1` |
| S | هزینهٔ ارسال | — | از پاسخ `shippingAmountInNoor` |
| R | نرخ ریال به‌ازای نور | — | `GET /api/settings/conversion-rate` ← `rialPerNoor` |

**قرارداد اجرا:**

- همهٔ فراخوانی‌های بای‌مانی توکن کاربر می‌خواهند: `POST http://localhost:1337/api/auth/password/login` با شماره و رمز خریدار، سپس در Swagger دکمهٔ Authorize.
- برای هر سبد یک UUID تازه بسازید (`[guid]::NewGuid()`) و تا پایان همان بخش همان را بفرستید. `items` همیشه تایپ‌دار است: `{"externalId":…,"type":"course|course_chapter|product","quantity":n}`.
- پیش از هر بخش موجودی خریدار را بخوانید و به‌عنوان `b0` یادداشت کنید؛ انتظارها نسبت به `b0` نوشته شده‌اند، چون کاربران قدیمی ۵۰ نور هدیهٔ خوش‌آمد دارند و خریداران تازه ۰.
- «پیش‌نمایش» یعنی `POST /api/course-carts/preview` روی استرپی (نه بای‌مانی) با توکن همان کاربر.
- «CM» یعنی Content-Manager استرپی یا پنل مدیریت.

## کاربر

هر نقش یک خریدار تازه از `buyers.items` در `seed-state.json` است (رمز همه مانند `users[0].password`). ستون «بخش» می‌گوید هر خریدار کجا خرج می‌شود؛ خریدار را در بخش دیگر به‌کار نبرید.

| `key` | `title` | موجودی هدف | بخش |
|---|---|---|---|
| `courseBuyer` | خریدار دوره | ۱۰۰ | الف |
| `couponBuyer` | خریدار کوپن | ۱۰۰ | ب (گام ۱ تا ۶) |
| `chapterBuyer` | خریدار فصل | ۱۰۰ | ب (گام ۷ تا ۹)، ج |
| `freeBuyer` | خریدار رایگان | ۰ | ب (گام ۸ تا ۱۰)، د |
| `productBuyer` | خریدار کالا | ۱۰۰ | ه، ح (گام ۶) |
| `mixedBuyer` | خریدار سبد مختلط | ۱۰۰ | و |
| `shortfallBuyer` | خریدار کم‌موجودی | ۰ | ز |
| `stockBuyer` | خریدار موجودی کالا | ۱۰۰ | ح |
| ادمین | کاربر `admin` در `users` | — | ز (گام ۶ و ۷، ۱۱) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `fixtures.*` طبق جدول نمادها.
- `buyers.generatedAtUtc` و `buyers.items[]`: برای هر خریدار `key`، `title`، `phone`، `externalUserId`، `internalUserId`، `targetBalanceNoor` و `hasAddress`. `hasAddress=false` یعنی سید بدون ادمین اجرا شده و هیچ سبدی که کالا دارد آماده نمی‌شود.
- شناسهٔ مبدأ ارسال: `fixtures.shippingOriginDocumentId`؛ باید در پاسخ سبدِ دارای کالا به‌صورت `shippingOrigin.id` برگردد.

## قدم‌ها

### الف — خرید دوره و خرید تکراری (`courseBuyer`)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger | `GET /api/wallet/balance` | موجودی `b0` (حداقل ۲۰)؛ یادداشت کنید |
| ۲ | Swagger | `GET /api/courses/{C1}/checkout` | `200`؛ `priceInNoor=20`، `purchaseStatus=NotPurchased`، `shortfallInNoor=0`؛ فقط استعلام است و چیزی ثبت نمی‌کند |
| ۳ | Swagger | `POST /api/carts/checkout` با `{cartId:X, items:[{C1, course, 1}], couponCode:null}` | `200`؛ `status=Ready`، `originalTotalInNoor=20`، `itemDiscountInNoor=0`، `couponDiscountInNoor=0`، `priceInNoor=20`، `shortfallInNoor=0`، `amountRial=0`، `topUpRequestId=null`؛ ردیف: `itemType=Course`، `priceInNoor=20`، `quantity=1`، `lineTotalInNoor=20`، `purchaseStatus=NotPurchased` |
| ۴ | Swagger | `GET /api/courses/{C1}/checkout` و `GET /api/wallet/balance` | هنوز `NotPurchased` و موجودی `b0`: آماده‌سازی سبد خرید یا برداشت نیست |
| ۵ | Swagger | تکرار همان درخواست گام ۳ | `200` با همان `cartId`، `status=Ready` و همان قیمت؛ رزرو دوم ساخته نمی‌شود |
| ۶ | Swagger | `POST /api/carts/{X}/purchase` بدون بدنه | `200`؛ `status=Paid`، ردیف `purchaseStatus=Completed` (اگر همگام‌سازی لحظه‌ای نبود `DeliveryPending` و با `GET` بعدی `Completed`)، `topUpRequestId=null` |
| ۷ | Swagger | تکرار گام ۶ | `200` با همان پاسخ `Paid`؛ موجودی دیگر کم نمی‌شود |
| ۸ | Swagger | `GET /api/wallet/balance`، `GET /api/wallet/transactions` | موجودی `b0−20`؛ یک ردیف `Purchase` با `amountNoor=−20` (بدهی) در بالای فهرست، نه دو تا |
| ۹ | Swagger | `GET /api/courses/{C1}/checkout` | `purchaseStatus=Completed` و قیمت Snapshot همان ۲۰ |
| ۱۰ | Swagger | `POST /api/carts/checkout` با `cartId` تازه و همان C1 | `409` با `{code:"CART_CONFLICT"}`؛ خرید تکراری دوره پیش از هر برداشت رد می‌شود |
| ۱۱ | مرورگر | ورود با همین خریدار ← «دوره‌های من» یا صفحهٔ دوره | دوره در فهرست خریدهای کاربر است و درس‌ها باز می‌شوند |

### ب — کوپن: تخفیف، پیش‌نمایش بدون مصرف، ظرفیت و آزادسازی رزرو

**تخفیف و تکرار پرداخت (`couponBuyer`)**

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | CM | خواندن رکورد K10 | `usedCount` را به‌عنوان `u0` یادداشت کنید |
| ۲ | Swagger استرپی | پیش‌نمایش `{items:[{C1, course, 1}], couponCode:"MANUALTEST10"}` | `200`؛ `priceInNoor=18`، `originalTotalInNoor=20`، `couponDiscountInNoor=2`؛ بدون `cartId`، `purchaseStatus` یا رزرو |
| ۳ | CM | خواندن رکورد K10 | `usedCount` هنوز `u0`: پیش‌نمایش مصرف نمی‌کند |
| ۴ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{C1, course, 1}], couponCode:"MANUALTEST10"}` | `200`؛ `status=Ready`، `couponCode=MANUALTEST10`، `originalTotalInNoor=20`، `couponDiscountInNoor=2`، `priceInNoor=18`؛ قیمت ردیف `20` می‌ماند (تخفیف فقط در خلاصهٔ سبد) |
| ۵ | CM | خواندن رکورد K10 | `usedCount=u0+1` (رزرو) |
| ۶ | بای‌مانی | کد نامعتبر: `cartId` تازه با `couponCode:"NOPE123"` | `400` با `{code:"CART_INVALID"}`. پیام مشخص («کد تخفیف … یافت نشد») فقط در پیش‌نمایش استرپی دیده می‌شود؛ checkout بای‌مانی پیام عمومی «درخواست یا قیمت سبد معتبر نیست.» می‌دهد |
| ۷ | بای‌مانی | دو درخواست **هم‌زمان** `POST /api/carts/{X}/purchase` | هر دو `200` و `status=Paid`؛ موجودی فقط `18` کم شده (نه ۳۶)؛ یک ردیف `Purchase` با `amountNoor=−18` |
| ۸ | CM | خواندن رکورد K10 | `usedCount=u0+1`: commit مصرف را دو بار نمی‌شمارد |

**ظرفیت محدود و آزادسازی (`chapterBuyer` و `freeBuyer`)** — از کد استخراج شده، هنوز اجرا نشده.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۹ | بای‌مانی با `chapterBuyer` | `POST /api/carts/checkout` با `{cartId:X3, items:[{C1, course, 1}], couponCode:"MANUALTEST1"}` | `200`؛ `status=Ready`، `priceInNoor=18`؛ در CM: `usedCount` کوپن K1 برابر ۱ |
| ۱۰ | بای‌مانی با `freeBuyer` | همان اقلام و `MANUALTEST1` با `cartId:X4` تازه | `400` با `{code:"CART_INVALID"}`: بررسی اعتبار کوپن پیش از رزرو ظرفیت را می‌بیند و سبد دوم رزرو نمی‌شود. K1 هنوز ۱ |
| ۱۱ | بای‌مانی با `chapterBuyer` | `POST /api/carts/{X3}/cancel` | `200`؛ `status=Failed`؛ در CM: `usedCount` کوپن K1 برابر ۰ (رزرو آزاد شد) |
| ۱۲ | بای‌مانی با `freeBuyer` | تکرار گام ۱۰ با همان `cartId:X4` | `200`؛ `status=Ready`، `priceInNoor=18`؛ K1 برابر ۱ |
| ۱۳ | بای‌مانی با `freeBuyer` | `POST /api/carts/{X4}/cancel` | `200`؛ `status=Failed`؛ K1 برابر ۰ (تا `verify` سبز بماند) |

### ج — فصل و دورهٔ فصلی (`chapterBuyer`)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{CH1, course_chapter, 1}], couponCode:null}` | `200`؛ `status=Ready`، `priceInNoor=10`؛ ردیف: `itemType=CourseChapter`، `externalItemId=CH1`، `parentExternalId=C2`، عنوان شامل نام فصل |
| ۲ | بای‌مانی | `POST /api/carts/{X}/purchase` | `200`؛ `status=Paid`، ردیف `Completed`؛ موجودی `b0−10` |
| ۳ | بای‌مانی | `GET /api/courses/{CH1}/checkout` | `200`؛ `purchaseStatus=Completed` |
| ۴ | CM یا مرورگر | رکورد کاربر در استرپی / صفحهٔ دوره | **فقط فصل ۱ باز است**: `enrolledChapterIntegrationIds` فقط CH1، و رابطهٔ کل دوره (C2) ساخته نشده. توجه: مسیر تک‌دورهٔ بای‌مانی فصلِ خریداری‌نشده را نمی‌شناسد و برای CH2 پاسخ `404` می‌دهد؛ شاهد را از رکورد کاربر یا مرورگر بگیرید |
| ۵ | بای‌مانی | `cartId` تازه با CH1 | `409` `{code:"CART_CONFLICT"}`: خرید تکراری فصل |
| ۶ | بای‌مانی | `cartId` تازه با `{C2, course, 1}` (خرید یکجای دوره پس از یک فصل) | **اختلاف کد و سند.** I09-CH این خرید را مجاز و به قیمت فصل‌های باقی‌مانده (۱۰ نور) می‌داند؛ کد فعلی `409` `{code:"CART_CONFLICT"}` می‌دهد، چون مالکیت هر فصل با شناسهٔ والد، خرید کل دوره را «خریده‌شده» می‌شمارد. تصمیم مالک (۲۰۲۶-۱۱-۲۵): نباید خطا بدهد و باید اجازهٔ خرید بدهد (به‌عنوان تسک P1 در `TASKS.md` ثبت شد؛ تا زمان پیاده‌سازی، کد فعلی `409` می‌دهد) |
| ۷ | بای‌مانی | `cartId` تازه با `{CH2, course_chapter, 1}` ← سپس `purchase` | `Ready` با `priceInNoor=10`، سپس `Paid`؛ موجودی `b0−20` |
| ۸ | بای‌مانی | `cartId` تازه با `{C2, course, 1}` (هر دو فصل خریده شده) | `409` `{code:"CART_CONFLICT"}`: حالت منفی خرید کامل دورهٔ فصلی‌فروش |
| ۹ | بای‌مانی | `cartId` تازه با CH2 | `409`؛ `GET /api/courses/{CH2}/checkout` ← `Completed` (فصل ردیف خرید خودش را دارد) |

### د — سفارش رایگان (`freeBuyer`، موجودی ۰)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | بای‌مانی | `GET /api/wallet/balance` | `b0` (برای خریدار تازه ۰) و تعداد ردیف‌های `/api/wallet/transactions` را یادداشت کنید |
| ۲ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{C2, course, 1}], couponCode:null}` | `200`؛ `status=Ready`، `originalTotalInNoor=0`، `itemDiscountInNoor=0`، `priceInNoor=0`، `shortfallInNoor=0`، `amountRial=0` |
| ۳ | بای‌مانی | `POST /api/carts/{X}/purchase` | `200`؛ `status=Paid`، ردیف `Completed`؛ خرید با موجودی صفر انجام می‌شود و رکورد خرید دارد |
| ۴ | بای‌مانی | `GET /api/wallet/balance`، `GET /api/wallet/transactions` | موجودی همان `b0`؛ ردیف تازه‌ای اضافه نشده (سفارش رایگان Ledger یا debit ندارد) |
| ۵ | بای‌مانی / مرورگر | `GET /api/courses/{C2}/checkout` | `Completed`؛ وضعیت فصل‌ها را از رکورد کاربر در استرپی (`enrolledChapters`) یا صفحهٔ دوره ببینید: خرید یکجای دوره ردیف خرید جدا برای فصل نمی‌سازد و مسیر تک‌دوره برای فصلِ بدون ردیف خرید `404` می‌دهد |
| ۶ | بای‌مانی | `cartId` تازه با CH1 | `409`: خرید فصل با والد خریده‌شده |
| ۷ | بای‌مانی | `cartId` تازه با C2 | `409` |

نکتهٔ سند: D18 می‌گوید سفارش رایگان از فیلد `isFree` استفاده می‌کند. در مسیر سبد، `isFree` خوانده نمی‌شود و «رایگان» یعنی قیمت پس از محاسبه صفر باشد (دورهٔ فصلی با قیمت پکیج ۰). برای این سناریو نتیجه یکی است، ولی دوره‌ای با `isFree=true` و قیمت غیرصفر از مسیر سبد رایگان نمی‌شود.

### ه — کالا با تعداد (`productBuyer`)

پیش از شروع از CM بخوانید: `F=fixedAmountInNoor`، `T=freeShippingThresholdInNoor`، `n0`= موجودی P1.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{P1, product, 2}], couponCode:null}` | `200`؛ `status=Ready`؛ ردیف `quantity=2`، `priceInNoor=30`، `lineTotalInNoor=60`؛ `originalTotalInNoor=60`؛ `shippingAmountInNoor=S` و `shippingRuleLabel`: اگر `T` خالی است یا `60 < T` ← `S=F` و «ارسال ثابت داخلی»، اگر `60 ≥ T` ← `S=0` و «ارسال رایگان»؛ `priceInNoor=60+S`؛ `shippingOrigin.id` برابر مبدأ کالا؛ `shippingAddress` همان آدرس پروفایل خریدار |
| ۲ | CM | موجودی P1 | `n0−2`: موجودی هنگام آماده‌سازی رزرو می‌شود |
| ۳ | بای‌مانی | `POST /api/carts/{X}/purchase` | `200`؛ `Paid`، ردیف `Completed`؛ موجودی `b0−(60+S)` |
| ۴ | CM | موجودی P1 | هنوز `n0−2`: commit کاهش دوم ندارد |
| ۵ | بای‌مانی | `GET /api/me/orders` | یک سفارش با `itemCount=1` و `finalAmountInNoor=60+S`، `financialStatus=Paid` |
| ۶ | بای‌مانی | `cartId` تازه با `{P1, product, 1}` | `200`؛ `Ready`: کالای فیزیکی را می‌شود دوباره خرید (برخلاف دوره) |
| ۷ | بای‌مانی | اعتبارسنجی: `quantity=0`؛ جفت تکراری (`{C1, course, 1}` دو بار)؛ `items:[]`؛ `cartId` تهی | هر چهار `400` با بدنهٔ اعتبارسنجی (فهرست `errors`) |

### و — سبد مختلط (`mixedBuyer`)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{C1, course, 1},{P1, product, 1}], couponCode:"MANUALTEST10"}` | `200`؛ `status=Ready`؛ دو ردیف (`Course` و `Product`)؛ `originalTotalInNoor=50`؛ `couponDiscountInNoor=2` (کوپن فقط روی دورهٔ مشمول، نه کالا)؛ `shippingAmountInNoor=S` یک بار؛ `priceInNoor=48+S` |
| ۲ | بای‌مانی | `POST /api/carts/{X}/purchase` | `200`؛ `Paid`؛ هر دو ردیف `Completed`؛ موجودی `b0−(48+S)` |
| ۳ | CM / بای‌مانی | موجودی P1؛ `GET /api/courses/{C1}/checkout` | موجودی فقط ۱ کم شده؛ دوره `Completed` |
| ۴ | بای‌مانی | `GET /api/me/orders` و `GET /api/me/orders/cart:{X}/receipt` | یک سفارش با دو قلم و `finalAmountInNoor=48+S`؛ رسید سهم نور عادی و هدیهٔ هر قلم را دارد |
| ۵ | مرورگر | `http://localhost:3000/checkout/result?source=noor&cartId={X}` با نشست همین خریدار | متن کلی «خرید شما با موفقیت ثبت شد»، نه متن مخصوص دوره (FE-ORDER-02) |
| ۶ | بای‌مانی | `cartId` تازه با `{C1, course, 1},{P1, product, 1}` (C1 الان خریده‌شده است) | `409` `{code:"CART_CONFLICT"}`؛ قلم نامعتبر کل خرید را متوقف می‌کند و موجودی P1 کم نمی‌شود (همه یا هیچ‌کدام) |

### ز — کسری موجودی کیف پول و شارژ (`shortfallBuyer`، موجودی ۰)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | بای‌مانی | `POST /api/carts/checkout` با `{cartId:X, items:[{C1, course, 1}], couponCode:null}` | `200`؛ `status=Ready`، `priceInNoor=20`، `balanceInNoor=b0`، `shortfallInNoor=20−b0`، `amountRial=(20−b0)×R` |
| ۲ | بای‌مانی | `POST /api/carts/{X}/purchase` | `400` با `{code:"CART_INSUFFICIENT_BALANCE", error:"موجودی نور برای خرید سبد کافی نیست."}` |
| ۳ | بای‌مانی | `GET /api/carts/{X}`، `GET /api/wallet/balance`، `GET /api/wallet/transactions` | سبد هنوز `Ready`؛ موجودی و گردش‌ها بدون تغییر؛ Ledger سفر ندارد |
| ۴ | بای‌مانی | `POST /api/carts/{X}/topup` بدون بدنه | `200`؛ `{topUpRequestId, clientReferenceId, amountRial}` با مبلغ برابر گام ۱ (مبلغ از سرور، نه مرورگر) |
| ۵ | بای‌مانی | `GET /api/carts/{X}` | `status=AwaitingTopUp`، `topUpRequestId` همان، `topUpStatus=Pending` |
| ۶ | بای‌مانی | تکرار گام ۴ | `200` با همان `topUpRequestId`؛ شارژ دوم ساخته نمی‌شود |
| ۷ | بای‌مانی | `POST /api/carts/{X}/purchase` | `409` `{code:"CART_CONFLICT"}`: شارژ تأیید نشده |
| ۸ | Swagger با توکن ادمین | `POST /api/admin/topups/{topUpRequestId}/confirm` با `{externalTransactionId:"…", confirmedAmount:20}` | `409` `{error:"روش پرداخت نامعتبر است."}`: شارژ درگاهی با تأیید کارت‌به‌کارت بسته نمی‌شود |
| ۹ | Swagger با توکن ادمین | `POST /api/admin/topups/{topUpRequestId}/reject` با `{reason:"تست دستی"}` | `204` |
| ۱۰ | Swagger با توکن ادمین | تکرار گام ۹ | `400` (درخواست در وضعیت `Rejected` قابل رد نیست) |
| ۱۱ | بای‌مانی | `GET /api/carts/{X}` | `AwaitingTopUp` با `topUpStatus=Rejected` (تا پردازشگر پس‌زمینه سبد را ببندد) |
| ۱۲ | بای‌مانی | تکرار گام ۱ با همان `cartId:X` | `200`؛ `status=Ready` با رزرو و قیمت تازه، `shortfallInNoor=20−b0`: همان سبد پس از شکست قطعی شارژ دوباره قابل پرداخت است |
| ۱۳ | بای‌مانی | `POST /api/carts/{X}/purchase` | هنوز `400` `{code:"CART_INSUFFICIENT_BALANCE"}` |
| ۱۴ | بای‌مانی | شارژ مستقل: `POST /api/topup/requests` با `{amount:20−b0, paymentMethod:2}`؛ سپس با ادمین `POST /api/admin/topups/{id}/confirm` با `confirmedAmount=20−b0` | `200` سپس `204`؛ `GET /api/wallet/balance` ← `b0` قدیمی + مبلغ شارژ (حداقل ۲۰) |
| ۱۵ | بای‌مانی | `POST /api/carts/{X}/purchase` | `200`؛ `Paid`، ردیف `Completed`؛ موجودی دقیقاً ۲۰ کم شده |
| ۱۶ | بای‌مانی | `POST /api/carts/{X}/cancel` | `200`؛ `status=Paid` بدون تغییر؛ لغو خرید دوره وجود ندارد (D09) |

### ح — کسری موجودی کالا (`stockBuyer` و `productBuyer`)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | CM | موجودی P2 | ۱ |
| ۲ | بای‌مانی با `stockBuyer` | `POST /api/carts/checkout` با `{cartId:X, items:[{P2, product, 2}], couponCode:null}` | `409` با `{code:"CART_STOCK_UNAVAILABLE", error:"تعداد درخواستی یکی از کالاهای سبد بیشتر از موجودی است؛ حداکثر ۱ عدد قابل خرید است.", lineError:{externalId:P2, type:"product", quantity:2, reason:"INSUFFICIENT_STOCK", availableQuantity:1}}` |
| ۳ | بای‌مانی / CM | `GET /api/carts/{X}`؛ موجودی P2؛ `GET /api/wallet/balance` | سبد `Failed` و ردیف `PurchaseIncomplete`؛ موجودی P2 هنوز ۱ (رزرو جزئی ندارد)؛ موجودی کیف پول بدون تغییر |
| ۴ | بای‌مانی با `stockBuyer` | `cartId` تازه با `{C1, course, 1},{P1, product, 1},{P2, product, 2}` | `409` با همان `CART_STOCK_UNAVAILABLE` روی P2؛ موجودی P1 کم نشده و C1 خریداری نشده: خطای یک قلم کل خرید را متوقف می‌کند |
| ۵ | بای‌مانی با `stockBuyer` | `cartId` تازه با `{P2, product, 1}` ← `purchase` | `Ready` با `priceInNoor=5+S`، سپس `Paid`؛ موجودی P2 در CM برابر ۰ |
| ۶ | بای‌مانی با `productBuyer` | `cartId` تازه با `{P2, product, 1}` | `409` با `CART_STOCK_UNAVAILABLE`، `error:"موجودی یکی از کالاهای سبد تمام شده است؛ آن را از سبد حذف کنید."`، `lineError.reason:"OUT_OF_STOCK"` و `availableQuantity:0` |
| ۷ | بای‌مانی با `productBuyer` | `POST /api/carts/checkout` با `{cartId:Y, items:[{P1, product, 1}], couponCode:"MANUALTEST10"}` | `400` `{code:"CART_INVALID"}`: کوپنِ فقط‌دوره روی سبدِ فقط‌کالا اعمال نمی‌شود؛ موجودی P1 کم نمی‌شود |

**بازگرداندن داده برای اجرای دوباره:** موجودی P2 بعد از گام ۵ صفر است و ویرایش `stock` در CM آن را برنمی‌گرداند، چون جدول موجودی رزرو ملاک است. اگر می‌خواهید بند ح را دوباره اجرا کنید، با ادمین قلم گام ۵ را لغو کنید (`GET /api/me/orders/{cartId}/receipt` با توکن خریدار ← `purchaseId`؛ سپس `POST /api/admin/physical-purchases/{purchaseId}/cancel` با `{operationId: UUID تازه, quantity:1, reason}` ← `outcome=Refunded` و برگشت موجودی P2 به ۱). معیارهای کامل لغو در سناریوی لغو است و اینجا فقط ابزار بازیابی داده است.

### ط — دسترسی و تکرار درخواست (هر خریدار)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بدون Authorize | `POST /api/carts/checkout`، `GET /api/carts/{X}`، `purchase`، `cancel`، `topup` | هر پنج `401` |
| ۲ | بای‌مانی با خریدار دیگر | `GET`، `purchase`، `cancel` و `topup` روی `cartId` سبد خریدار قبلی | هر چهار `404` `{code:"CART_NOT_FOUND"}`؛ سبد کاربر دیگر دیده نمی‌شود |
| ۳ | بای‌مانی | `GET /api/carts/{UUID تصادفی}` | `404` `{code:"CART_NOT_FOUND"}` |
| ۴ | بای‌مانی | `POST /api/carts/checkout` با همان `cartId` ولی اقلام متفاوت | `409` `{code:"CART_CONFLICT"}`؛ شناسهٔ سبد برای انتخاب تازه نیست |
| ۵ | بای‌مانی | دو `POST /api/carts/checkout` **هم‌زمان** با یک `cartId` و یک کوپن | هر دو `200` با همان `cartId` و `Ready`؛ `usedCount` کوپن فقط ۱ بیشتر شده (یک رزرو مشترک) |
| ۶ | بای‌مانی | `POST /api/carts/{X}/cancel` روی سبد `Ready`، سپس تکرار | اول `200` با `status=Failed` و ردیف `PurchaseIncomplete`؛ تکرار همان پاسخ (بی‌اثر) |
| ۷ | بای‌مانی | `GET /api/carts/{X}` و `POST /api/carts/{X}/purchase` پس از لغو | `Failed`؛ خرید `409` `{code:"CART_CONFLICT"}` |
| ۸ | بای‌مانی | `POST /api/carts/checkout` با همان `cartId` لغوشده | `200`؛ `Ready` دوباره (رزرو تازه) |
| ۹ | بای‌مانی | `POST /api/carts/checkout` با `externalId` ناموجود | `400` `{code:"CART_INVALID"}` |
| ۱۰ | بای‌مانی | `POST /api/carts/{X}/cancel` روی سبد `Paid` (الف) | `200`؛ `status=Paid` بدون تغییر و بدون برگشت نور |

## نتیجهٔ موردانتظار

- **قیمت و برداشت:** مجموع اقلام به‌اضافهٔ ارسال منهای تخفیف‌های سطح سبد برابر `priceInNoor` و برابر مبلغ کسرشده از کیف پول است. قیمت هر ردیف ثابت می‌ماند و تخفیف فقط در خلاصهٔ سبد است (I09-C).
- **مشاهده بی‌اثر:** استعلام تک‌دوره، `GET` سبد و پیش‌نمایش استرپی نه خرید و نه رزرو می‌سازند و کوپن مصرف نمی‌کنند؛ فقط کلیک پرداخت (checkout) رزرو می‌کند (تصمیم ۱۸ اکتبر در D08).
- **یک‌بار بودن:** تکرار یا هم‌زمانیِ `purchase` فقط یک برداشت و یک ردیف خرید می‌سازد؛ موجودی کالا یک بار کم می‌شود؛ commit کوپن مصرف را دو بار نمی‌شمارد (R06، E3).
- **خرید تکراری:** دوره و فصلِ خریداری‌شده پیش از برداشت با `409` رد می‌شود؛ کالای فیزیکی قابل خرید مجدد است (C9).
- **فصل:** خرید فصل فقط همان فصل را باز می‌کند و رابطهٔ کل دوره نمی‌سازد؛ خرید یکجای دوره هر دو فصل را باز می‌کند (I09-CH).
- **رایگان:** سفارش صفر با موجودی صفر `Paid` می‌شود، رکورد خرید دارد و Ledger یا debit ندارد.
- **کوپن:** تخفیف فقط روی اقلام مشمول، یک بار روی جمع آن‌ها؛ رزرو با لغو آزاد می‌شود و سقف مصرف رعایت می‌شود (C2).
- **کسری:** کمبود نور قبل از برداشت متوقف می‌شود و سبد `Ready` می‌ماند؛ شارژ سبد از سرور حساب می‌شود و تکرارش شارژ دوم نمی‌سازد؛ پس از رد شارژ همان سبد دوباره قابل پرداخت است (I09-C، R03).
- **موجودی کالا:** کمبود موجودی کل خرید را قبل از هر رزرو متوقف می‌کند و پیام، تعداد مجاز را می‌گوید؛ موجودی منفی یا فروش بیش از موجودی رخ نمی‌دهد (C7، I09-PH).
- **دسترسی:** هر سبد فقط برای خریدارش دیده می‌شود؛ نبود توکن `401` و سبد کاربر دیگر `404`.
- **دفتر کل (برای سبدهای پولی):** ردیف‌های هر سبد جفتی‌اند. با یک خواندن فقط‌خواندنی روی `ByeMoney_Dev` بررسی کنید که به‌ازای هر `BalanceType`، جمع مبلغ روی حساب خریدار با جمع روی حساب گیرنده برابر و برابر `priceInNoor` است:
  `SELECT "AccountId","BalanceType", sum("Amount") FROM "LedgerEntries" WHERE "TransactionId"='<cartId>' GROUP BY 1,2 ORDER BY 1,2;`

## ثبت نتیجه

- موفق: بخش «سبد و خرید» در [گزارش شواهد](../Verification-Summary.md) — تاریخ، بیلد، شمارهٔ خریداران دسته، `cartId` هر سبد پولی، موجودی P1/P2 پیش و پس.
- ناموفق: همان بخش — کد و بدنهٔ پاسخ، شمارهٔ گام، `cartId`، و آخرین خط `logs/log-*.txt` بای‌مانی. برای گام ۶ِ ج، پاسخ واقعی و ارجاع به اختلاف با I09-CH را بنویسید.
- اگر `verify` پیش از اجرا سبز نبود، اجرا را شروع نکنید؛ نتیجهٔ آن اجرا شاهد پذیرش نیست.

## خارج از دامنه / نیازمند تأیید

- **پرداخت واقعی درگاه (SEP) و خرید خودکار پس از callback:** این سناریو درگاه را صدا نمی‌زند. کسری با رد شارژ + شارژ مستقل ادمین بسته می‌شود. مسیر «تأیید بانکی ← خرید خودکار» (R03) ماک یا تأیید جداگانه می‌خواهد.
- **پستکس و کف ارزش اعلامی:** اگر `costMode=postex` است، هر سبد کالای فیزیکی استعلام واقعی می‌زند ← پیش از اجرای ه، و و ح تأیید بگیرید یا موقتاً `fixed` کنید. پستکس برای `total_value` زیر کف خود (در سنجش محلی `10,000` ریال رد و `120,000` ریال پذیرفته شد) پیام «هیچ سرویس دهندهٔ پستی امکان ارسال این مرسوله را ندارد» می‌دهد؛ با `rialPerNoorForShipping=10` کالاهای `5` و `30` نوری زیر کف‌اند، ولی با `12,000` نور (`120,000` ریال) استعلام زنده موفق می‌شود. ساخت مرسوله در این سناریو انجام نمی‌شود.
- **آستانهٔ ارسال رایگان و چند مبدأ:** فقط در حد نمایش برچسب در ه دیده می‌شود؛ پذیرش کامل در SHIP-COST-MODE و SHIP-MULTI-ORIGIN.
- **لغو سفارش فیزیکی:** فقط به‌عنوان ابزار بازیابی داده در ح آمده؛ معیارهایش در سناریوی لغو (I09-PC) است.
- **مرورگر:** فقط سه نگاه (دوره‌های من، صفحهٔ دورهٔ فصلی، صفحهٔ نتیجهٔ خرید مختلط) در این سناریو هست؛ جریان کامل سبد در فرانت (`/cart`، انتخاب آدرس، تغییر انتخاب و سبد جایگزین) سناریوی خودش را می‌خواهد.
- پیامک و ایمیل: این سناریو هیچ‌کدام را نمی‌فرستد.
