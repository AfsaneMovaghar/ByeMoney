# ۰۴ — ارسال: هزینهٔ ثابت، ثبت دستی کد رهگیری، تحویل، تأیید خریدار، برگشتی و هشدار پستی

> دامنه: محاسبهٔ هزینهٔ ثابت ارسال و آستانهٔ ارسال رایگان، ثبت دستی کد رهگیری، تغییر وضعیت تحویل توسط ادمین، تأیید دریافت توسط خریدار، تعیین‌تکلیف مرسولهٔ برگشتی و هشدار پستی (خسارت/مفقودی)، به‌همراه خطاها، دسترسی غیرمجاز و تکرار درخواست. قالب: [README](README.md).
> معیارها از I11-O (بندهای وضعیت تحویل، کد رهگیری، تأیید خریدار، `SHIP-RETURN-RESOLUTION`، `SHIP-FIX-2`، ارسال و رسید) در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، [ماتریس دسترسی](../../architecture/Permissions-Matrix.md)، D08 و D10 در [تصمیم‌ها](../../architecture/ByeMoney-Decisions.md) و ردیف‌های `SHIP-COST-MODE`، `پذیرش زندهٔ SHIP-TRACKING / B / C`، `پذیرش SHIP-FIX-2`، `پذیرش SHIP-MULTI-ORIGIN` و `رسید ادمین و تغییر وضعیت تحویل (I11-O)` در [TASKS](../../../TASKS.md).

## پیش‌نیاز

- چهار سرویس بالا باشند (استرپی `:1337`، بای‌مانی `:5000`، فرانت `:3000`، PostgreSQL).
- `.\seed.ps1 -Action up` **با ادمین استرپی** اجرا شده باشد؛ موجودی `P1` حداقل ۸ عدد و آدرس خریداران ثبت شده باشد.
- `.\seed.ps1 -Action buyers` **یک بار پیش از اجرای این سناریو** اجرا شده و `.\seed.ps1 -Action verify` سبز باشد.
- تنظیمات ارسال استرپی (`api::shipping-setting.shipping-setting`) روی حالت **هزینهٔ ثابت** باشد:
  - `costMode = "fixed"`
  - `fixedAmountInNoor` پر (مثلاً `5` نور؛ این مقدار را به‌عنوان `S` یادداشت کنید)
  - `freeShippingThresholdInNoor` پر (مثلاً `100` نور) یا `null` (برای گام آستانهٔ رایگان، موقتاً مقدار را از قیمت یک عدد `P1` بزرگ‌تر و از دو عدد `P1` کوچک‌تر بگذارید، مثلاً `50`)
  - `rialPerNoorForShipping` پر (مثلاً `10`)

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` بردارید، حدس نزنید):

| نماد | چیست | کلید در `fixtures` | مقدار ثابت |
|---|---|---|---|
| P1 | کالای فیزیکی، ۳۰ نور، موجودی ۱۰ | `physicalProductDocumentId` | — |
| O1 | مبدأ ارسال | `shippingOriginDocumentId` | — |
| S | هزینهٔ ثابت ارسال | — | از `shipping-setting.fixedAmountInNoor` |

**قرارداد اجرا:**

- توکن هر کاربر از `POST http://localhost:1337/api/auth/password/login` گرفته و در Swagger با دکمهٔ **Authorize** (`Bearer <توکن>`) گذاشته می‌شود.
- برای هر سبد یک UUID تازه بسازید (`[guid]::NewGuid()`).
- پیش از خرید هر سبد موجودی خریدار را با `GET /api/wallet/balance` بخوانید و به‌عنوان `b0` یادداشت کنید.

## کاربر

| نقش | شماره / منبع | رمز | کاربرد در این سناریو |
|---|---|---|---|
| ادمین | `09120000003` (`users[key=admin]`) | `Manual-Test-2026` | بخش‌های ب، ج، د، هـ (دارای `Admin` در بای‌مانی و `administrator` در استرپی) |
| کاربر عادی | `09120000001` (`users[key=funded]`) | `Manual-Test-2026` | خطاهای `403` در مسیرهای ادمین و `404` روی سفارش دیگران |
| کاربر بدون آدرس | `09120000002` (`users[key=empty]`) | `Manual-Test-2026` | در صورت نداشتن آدرس، تست خطای `400 CART_INVALID` (در غیر این صورت با کاربر تازهٔ بدون آدرس) |
| `shippingDeliveryBuyer` | از `buyers.items` (هدف ۱۰۰ نور) | مانند `users[0].password` | بخش‌های الف، ب و ج (هزینهٔ ثابت، کد رهگیری دستی، تأیید خریدار و تحویل ادمین) |
| `shippingReturnBuyer` | از `buyers.items` (هدف ۱۰۰ نور) | مانند `users[0].password` | بخش د (مرسولهٔ برگشتی و دو حالت `DeliveryConfirmed` و `ClosedWithoutSettlement`) |
| `shippingAlertBuyer` | از `buyers.items` (هدف ۱۰۰ نور) | مانند `users[0].password` | بخش هـ (هشدار پستی `11304`، تأیید خریدار با هشدار باز و تعیین‌تکلیف ادمین) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `fixtures.physicalProductDocumentId` (`P1`) و `fixtures.shippingOriginDocumentId` (`O1`).
- `users[key=admin|funded].externalUserId`.
- `buyers.items[]` برای نقش‌های `shippingDeliveryBuyer`، `shippingReturnBuyer` و `shippingAlertBuyer` (`phone`، `externalUserId`، `internalUserId`، `hasAddress`).

## قدم‌ها

### الف — ارسال با هزینهٔ ثابت (`costMode = fixed`) و آستانهٔ رایگان (`shippingDeliveryBuyer`)

> فرض مثال در این بخش: قیمت `P1 = 30`، `fixedAmountInNoor = S` (مثلاً `5`) و `freeShippingThresholdInNoor = 50`. اگر آستانهٔ رایگان در محیط شما عدد دیگری است، تعداد `P1` را متناسب با آن انتخاب کنید. یک UUID تازه به‌نام `C_D1` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `shippingDeliveryBuyer` | `POST /api/carts/checkout` با `{cartId: C_D1, items:[{externalId: P1, type:"product", quantity: 1}], couponCode: null}` | `200`؛ `status="Ready"`، `originalTotalInNoor=30`، `shippingAmountInNoor=S`، `priceInNoor=30+S` |
| ۲ | Swagger با `shippingDeliveryBuyer` | (آستانهٔ رایگان) `POST /api/carts/checkout` با UUID تازهٔ `C_FREE` و `{items:[{externalId: P1, type:"product", quantity: 2}]}` (جمع `60 >= 50`) | `200`؛ `originalTotalInNoor=60`، `shippingAmountInNoor=0`، `priceInNoor=60` |
| ۳ | Swagger با کاربر بدون آدرس | `POST /api/carts/checkout` با UUID تازه و `{items:[{externalId: P1, type:"product", quantity: 1}]}` | `400` با `{code:"CART_INVALID"}` |
| ۴ | Swagger با `shippingDeliveryBuyer` | پرداخت سبد اول: `POST /api/carts/{C_D1}/purchase` | `200`؛ `status="Paid"`؛ موجودی برابر `b0 − (30 + S)`؛ `physicalDeliveryStatus="Preparing"` |
| ۵ | Swagger با `shippingDeliveryBuyer` | **تکرار دقیق** گام ۴: `POST /api/carts/{C_D1}/purchase` | `200` با همان پاسخ `Paid`؛ موجودی دیگر کم نمی‌شود |
| ۶ | Swagger با `shippingDeliveryBuyer` | `GET /api/me/orders/cart:{C_D1}/receipt` | `200`؛ `cartSummary.shippingAmountInNoor = S`، `finalAmountInNoor = 30 + S`، و `purchaseId` قلم کالا را یادداشت کنید (`PD1`) |

### ب — ثبت دستی کد رهگیری و خطاهای تغییر وضعیت (`admin` و `funded`)

> از `purchaseId = PD1` (در وضعیت `Preparing`) و سفارش `cart:{C_D1}` استفاده کنید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن `funded` (کاربر عادی) | `PUT /api/admin/physical-purchases/{PD1}/delivery-status` با `{"status":"Shipped","trackingCode":"1234567890123","trackingProvider":"Manual"}` | `403 Forbidden` |
| ۲ | Swagger با توکن `admin` | شناسهٔ ناموجود: `PUT /api/admin/physical-purchases/11111111-1111-4111-8111-111111111111/delivery-status` با همان بدنه | `404 Not Found` |
| ۳ | Swagger با توکن `admin` | پرش وضعیت: `PUT /api/admin/physical-purchases/{PD1}/delivery-status` با `{"status":"Delivered"}` | `409 Conflict` (گذار از `Preparing` به `Delivered` مجاز نیست؛ در کد با `ConflictDomainException` به `409` نگاشت می‌شود) |
| ۴ | Swagger با توکن `admin` | ارسال بدون کد واقعی: (الف) `{"status":"Shipped","trackingCode":""}`؛ (ب) `{"status":"Shipped","trackingCode":"***"}` (یا `"---"`)؛ (ج) کد ۵۱ نویسه‌ای؛ (د) `{"status":"Shipped","trackingCode":"1234567890123","trackingProvider":"Manual","providerParcelId":"P-1"}` | هر چهار مورد `400 Bad Request`؛ وضعیت همچنان `Preparing` |
| ۵ | Swagger با توکن `admin` | ثبت موفق دستی: `PUT /api/admin/physical-purchases/{PD1}/delivery-status` با `{"status":"Shipped","trackingCode":"1234567890123","trackingProvider":"Manual"}` | `200`؛ `deliveryStatus="Shipped"`، `trackingCode="1234567890123"`، `trackingProvider="Manual"` و `shippedAtUtc` پر است (این مقدار را یادداشت کنید) |
| ۶ | Swagger با توکن `admin` | **اصلاح کد در تکرار `Shipped`:** `PUT /api/admin/physical-purchases/{PD1}/delivery-status` با `{"status":"Shipped","trackingCode":"9876543210987","trackingProvider":"Manual"}` | `200`؛ `trackingCode="9876543210987"` ولی `shippedAtUtc` **تغییر نمی‌کند** |
| ۷ | Swagger با توکن `admin` | **تکرار بدون کد:** `PUT /api/admin/physical-purchases/{PD1}/delivery-status` با `{"status":"Shipped"}` و همچنین ارسال با کد ستاره‌ای `{"status":"Shipped","trackingCode":"***"}` | اولی `200` (کد `9876543210987` حفظ می‌شود)؛ دومی `400` و کد ذخیره‌شده حفظ می‌شود |
| ۸ | Swagger با `shippingDeliveryBuyer` | `GET /api/me/orders/cart:{C_D1}` | `200`؛ `trackingCode="9876543210987"`، `trackingProvider="Manual"`، `trackingUrl=null` و فیلد `providerParcelId` در پاسخ نیست |

### ج — تحویل توسط ادمین، تأیید دریافت توسط خریدار و قفل پس از تحویل

> گام‌های ۱ تا ۵ روی سفارش `cart:{C_D1}` (`PD1` در وضعیت `Shipped`)؛ گام‌های ۶ و ۷ روی یک سفارش تازهٔ `C_D2` برای `shippingDeliveryBuyer` (با ۱ عدد `P1`) اجرا می‌شوند تا مسیر تحویل مستقیم ادمین هم آزموده شود.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `funded` (کاربر دیگر) | `POST /api/me/orders/cart:{C_D1}/confirm-delivery` | `404 Not Found` (سفارش کاربر دیگر و شناسهٔ نامعتبر `404` می‌دهند) |
| ۲ | Swagger با `shippingDeliveryBuyer` | پیش از ارسال (سفارش جدید `C_D2` در وضعیت `Preparing`): `POST /api/me/orders/cart:{C_D2}/confirm-delivery` | `200`؛ `{orderId:"cart:{C_D2}", confirmedCount:0, purchaseIds:[]}` |
| ۳ | مرورگر (`/profile/orders`) یا Swagger با `shippingDeliveryBuyer` | تأیید دریافت سفارش `cart:{C_D1}`: دکمهٔ «تحویل گرفتم» یا `POST /api/me/orders/cart:{C_D1}/confirm-delivery` | `200`؛ `confirmedCount=1`، `purchaseIds=[PD1]`؛ در جزئیات سفارش `physicalDeliveryStatus="Delivered"` و `deliveredAtUtc` پر است |
| ۴ | Swagger با `shippingDeliveryBuyer` | **تکرار تأیید خریدار:** `POST /api/me/orders/cart:{C_D1}/confirm-delivery` | `200`؛ `confirmedCount=0`، `purchaseIds=[]` (بی‌اثر و بدون تغییر زمان تحویل) |
| ۵ | Swagger با توکن `admin` | قفل پس از تحویل: (الف) `PUT .../PD1/delivery-status` با `{"status":"Delivered"}`؛ (ب) با `{"status":"Shipped","trackingCode":"1111111111111"}` | (الف) `200` (تکرار بی‌اثر)؛ (ب) `409 Conflict` (بازگشت از `Delivered` ممنوع است) |
| ۶ | Swagger با توکن `admin` | سفارش `C_D2` (`PD2`): ابتدا `Shipped` با کد `"2222222222222"` و `Manual` ← سپس `PUT /api/admin/physical-purchases/{PD2}/delivery-status` با `{"status":"Delivered"}` | هر دو `200`؛ `deliveryStatus="Delivered"` و `deliveredAtUtc` پر است؛ موجودی/Ledger تغییر نمی‌کند |

### د — مرسولهٔ برگشتی و تعیین‌تکلیف ادمین (`shippingReturnBuyer`)

> برای پوشش هر دو نتیجهٔ `DeliveryConfirmed` و `ClosedWithoutSettlement`، دو سفارش مجزا (`C_R1` با قلم `PR1` و `C_R2` با قلم `PR2`) برای `shippingReturnBuyer` بخرید و هر دو را با کد رهگیری دستی `Shipped` کنید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن `admin` | پیش از ثبت برگشتی: `POST /api/admin/physical-purchases/{PR1}/carrier-return/resolve` با `{"resolution":"DeliveryConfirmed","note":"تست"}` | `400 Bad Request` (قلم هنوز برگشتی نیست) |
| ۲ | شل پاورشل (`seed.ps1`) | ثبت غیرمالی پرچم برگشتی روی هر دو قلم `Shipped`: `.\seed.ps1 -Action shipping-flags -PurchaseId {PR1} -CarrierFlag return` و تکرار برای `{PR2}` | هر دو موفق؛ `CarrierReturnedAtUtc` پر می‌شود بدون هیچ تغییر در `Ledger`/`Wallet` |
| ۳ | Swagger با `shippingReturnBuyer` | `POST /api/me/orders/cart:{C_R1}/confirm-delivery` | `409 Conflict` (برگشتی مانع تأیید خریدار است) |
| ۴ | Swagger با توکن `admin` | `PUT /api/admin/physical-purchases/{PR1}/delivery-status` با `{"status":"Delivered"}` | `400 Bad Request` (تحویل دستی تا پیش از تعیین‌تکلیف برگشتی مسدود است) |
| ۵ | Swagger با `funded` / `admin` | خطاهای اعتبارسنجی: (الف) توکن `funded` روی `carrier-return/resolve`؛ (ب) توکن `admin` با `note: ""` | (الف) `403 Forbidden`؛ (ب) `400 Bad Request` |
| ۶ | مرورگر (`/profile/orders` و `/admin/orders`) | مشاهدهٔ هر دو سفارش در پروفایل خریدار و کارت سفارش ادمین | نشان برگشتی دیده می‌شود؛ در `GET /api/admin/orders` مقدار `carrierReturnStatus="PendingReview"` است |
| ۷ | Swagger با توکن `admin` | تعیین‌تکلیف اول (`DeliveryConfirmed`): `POST /api/admin/physical-purchases/{PR1}/carrier-return/resolve` با `{"resolution":"DeliveryConfirmed","note":"تأیید تحویل پس از پیگیری"}` | `200`؛ `deliveryStatus="Delivered"`، `deliveredAtUtc` پر و `carrierReturnResolution="DeliveryConfirmed"` |
| ۸ | Swagger با توکن `admin` | **تکرار دقیق و متعارض روی `PR1`:** (الف) تکرار دقیق گام ۷؛ (ب) ارسال همان مسیر با `{"resolution":"ClosedWithoutSettlement","note":"تغییر"}` یا یادداشت متفاوت | (الف) `200` (بی‌اثر)؛ (ب) `409 Conflict` |
| ۹ | Swagger با توکن `admin` | تعیین‌تکلیف دوم (`ClosedWithoutSettlement`): `POST /api/admin/physical-purchases/{PR2}/carrier-return/resolve` با `{"resolution":"ClosedWithoutSettlement","note":"برگشت به انبار و بسته بدون تسویه"}` | `200`؛ `deliveryStatus="Shipped"` می‌ماند؛ در `GET /api/admin/orders` مقدار `carrierReturnStatus="ClosedWithoutSettlement"` است |
| ۱۰ | Swagger با توکن `admin` | تلاش تحویل روی `PR2` پس از `ClosedWithoutSettlement`: `PUT /api/admin/physical-purchases/{PR2}/delivery-status` با `{"status":"Delivered"}` | `400 Bad Request` (برای همیشه از تحویل و تسویه خارج است) |

### هـ — هشدار پستی خسارت یا مفقودی (`shippingAlertBuyer`)

> دو سفارش مجزا (`C_A1` با قلم `PA1` و `C_A2` با قلم `PA2`) برای `shippingAlertBuyer` بخرید و هر دو را با کد رهگیری دستی `Shipped` کنید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن `admin` | پیش از ثبت هشدار: `POST /api/admin/physical-purchases/{PA1}/carrier-alert/resolve` با `{"resolution":"DeliveryConfirmed","note":"تست"}` | `400 Bad Request` |
| ۲ | شل پاورشل (`seed.ps1`) | ثبت غیرمالی هشدار `11304`: `.\seed.ps1 -Action shipping-flags -PurchaseId {PA1} -CarrierFlag alert -AlertCode 11304` و تکرار برای `{PA2}` | هر دو موفق؛ `CarrierAlertCode=11304` بدون تغییر `DeliveryStatus` و بدون اثر مالی |
| ۳ | Swagger با `shippingAlertBuyer` | **تفاوت با برگشتی — تأیید خریدار روی `PA1` با وجود هشدار باز:** `POST /api/me/orders/cart:{C_A1}/confirm-delivery` | `200`؛ `confirmedCount=1` و وضعیت تحویل `Delivered` می‌شود؛ اما در `GET /api/admin/orders` هنوز `carrierAlertStatus="PendingReview"` است |
| ۴ | Swagger با `shippingAlertBuyer` | بررسی محرمانگی در `GET /api/me/orders/cart:{C_A1}` و `receipt` | `carrierAlertCode` و یادداشت تعیین‌تکلیف به خریدار برنمی‌گردد |
| ۵ | Swagger با توکن `admin` | بستن هشدار `PA1` با `ClosedWithoutSettlement`: `POST /api/admin/physical-purchases/{PA1}/carrier-alert/resolve` با `{"resolution":"ClosedWithoutSettlement","note":"خسارت بررسی و بسته شد"}` | `200`؛ هشدار بسته می‌شود و `deliveryStatus` به عقب برنمی‌گردد (`Delivered` می‌ماند) |
| ۶ | Swagger با توکن `admin` | بستن هشدار `PA2` (که هنوز `Shipped` است) با `DeliveryConfirmed`: `POST /api/admin/physical-purchases/{PA2}/carrier-alert/resolve` با `{"resolution":"DeliveryConfirmed","note":"تحویل تأیید شد"}` | `200`؛ `deliveryStatus="Delivered"` و `deliveredAtUtc` ثبت می‌شود |
| ۷ | Swagger با توکن `admin` / `funded` | (الف) تکرار دقیق گام ۶؛ (ب) تکرار با `note` یا `resolution` متفاوت؛ (ج) یادداشت خالی؛ (د) توکن `funded` | (الف) `200`؛ (ب) `409 Conflict`؛ (ج) `400`؛ (د) `403 Forbidden` |

## نتیجهٔ موردانتظار

- هزینهٔ ثابت ارسال و آستانهٔ رایگان دقیقاً مطابق `shipping-setting` استرپی در `checkout` و رسید ثبت و یک بار از کیف پول کسر شود.
- ثبت دستی کد رهگیری فقط با کد معتبر انجام شود، `shippedAtUtc` در اصلاح کد دست نخورد، و پس از `Delivered` بازگشت به عقب مسدود باشد.
- تأیید خریدار تکرارپذیر باشد، در حالت برگشتی `409` بدهد، و در حالت هشدار پستی همچنان باز بماند بدون آن‌که هشدار را ببندد یا اطلاعات داخلی آن را به خریدار نشان دهد.
- تعیین‌تکلیف برگشتی و هشدار هر دو تکرارپذیر (`200`) و در تعارض (`409`) ایمن باشند و هیچ‌کدام سطر `Ledger` یا موجودی کیف پول را تغییر ندهند.

## ثبت نتیجه

- موفق: [گزارش شواهد](../Verification-Summary.md) — تاریخ، نسخهٔ بیلد، `cartId`/`purchaseId` و اسکرین‌شات پروفایل و پنل ادمین.
- ناموفق: همان‌جا — کد خطا، پاسخ API، لاگ سرور.

## خارج از دامنه / نیازمند تأیید

- `costMode = "postex"`، ساخت مرسوله نزد پستکس (`POST /api/admin/carts/{cartId}/shipments`)، دکمهٔ «بررسی وضعیت» (`POST /api/admin/physical-purchases/{purchaseId}/shipments/refresh`) و همگام‌سازی خودکار پس‌زمینه (`PhysicalDeliverySyncService`) به‌دلیل نیاز به فراخوانی واقعی پستکس خارج از این سناریو هستند و اجرای زندهٔ آن‌ها نیازمند تأیید جداگانه است.
