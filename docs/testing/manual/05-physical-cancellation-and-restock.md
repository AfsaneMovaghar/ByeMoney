# ۰۵ — لغو کالای فیزیکی و برگشت نور و موجودی: لغو کاربر، لغو جزئی ادمین، لغو پس از ارسال

> دامنه: پیش‌نمایش و لغو کل بخش فیزیکی سبد توسط کاربر (پرداخت‌نشده و پرداخت‌شده)، لغو قلم یا تعداد توسط ادمین (گردکردن و باقیماندهٔ آخرین واحد، تفکیک سهم عادی/هدیه و قاعدهٔ برگشت ارسال)، ممنوعیت لغو پس از ارسال (`Shipped`/`Delivered`)، برگشت موجودی در استرپی (`restock`) و صف ناتمام‌ها، به‌همراه خطاها، دسترسی غیرمجاز و تکرار/تعارض `operationId`. قالب: [README](README.md).
> معیارها از `I09-PC`، `I11-P` و `I11-T`/`I11-O` در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، [ماتریس دسترسی](../../architecture/Permissions-Matrix.md)، `D09` در [تصمیم‌ها](../../architecture/ByeMoney-Decisions.md) و ردیف `C8` و بخش `پذیرش زندهٔ SHIP-FIX-1` در [TASKS](../../../TASKS.md).

## پیش‌نیاز

- چهار سرویس بالا باشند (استرپی `:1337`، بای‌مانی `:5000`، فرانت `:3000`، PostgreSQL).
- `.\seed.ps1 -Action up` **با ادمین استرپی** اجرا شده باشد؛ موجودی `P1` حداقل ۶ عدد و آدرس خریداران ثبت شده باشد.
- `.\seed.ps1 -Action buyers` **یک بار پیش از اجرای این سناریو** اجرا شده و `.\seed.ps1 -Action verify` سبز باشد.
- تنظیمات ارسال استرپی (`api::shipping-setting.shipping-setting`) روی حالت `costMode = "fixed"` با `fixedAmountInNoor` غیرصفر (مثلاً `5` نور؛ به‌عنوان `S` یادداشت کنید) و `freeShippingThresholdInNoor` مثلاً `100` نور باشد.

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` بردارید، حدس نزنید):

| نماد | چیست | کلید در `fixtures` | مقدار ثابت |
|---|---|---|---|
| P1 | کالای فیزیکی، ۳۰ نور، موجودی ۱۰ | `physicalProductDocumentId` | — |
| K1 | دورهٔ پایه، ۲۰ نور | `basicCourseDocumentId` | — |
| CPN | کوپن ۱۰٪ روی دوره‌ها | `couponCode` | `MANUALTEST10` |
| S | هزینهٔ ثابت ارسال | — | از `shipping-setting.fixedAmountInNoor` |

**قرارداد اجرا:**

- توکن هر کاربر از `POST http://localhost:1337/api/auth/password/login` گرفته و در Swagger با دکمهٔ **Authorize** (`Bearer <توکن>`) گذاشته می‌شود.
- برای هر سبد یک UUID تازه بسازید (`[guid]::NewGuid()`) و برای هر اقدام لغو یک `operationId` یکتا (`[guid]::NewGuid()`) بسازید و برای آزمایش تکرار، همان `operationId` را نگه دارید.
- پیش از هر بخش، موجودی کیف پول خریدار را با `GET /api/wallet/balance` (`balance` و `giftBalance`) و موجودی فعلی `P1` در استرپی (`stock`) را یادداشت کنید.

## کاربر

| نقش | شماره / منبع | رمز | کاربرد در این سناریو |
|---|---|---|---|
| ادمین | `09120000003` (`users[key=admin]`) | `Manual-Test-2026` | دارای `Noor.Inject` برای لغو جزئی، `Courses.Manage` برای ارسال دستی، و `Purchases.View` برای فهرست سفارش‌ها |
| کاربر عادی | `09120000001` (`users[key=funded]`) | `Manual-Test-2026` | خطاهای `403 Forbidden` روی مسیرهای ادمین و `404` روی سفارش دیگران |
| `cancelBuyer` | از `buyers.items` (هدف ۱۵۰ نور عادی، ۰ هدیه) | مانند `users[0].password` | بخش الف (لغو سبد پرداخت‌نشده، لغو کامل سبد مختلط با کوپن، تکرار و تعارض `operationId`) |
| `cancelPartialBuyer` | از `buyers.items` (هدف ۱۰۰ نور عادی + ۱۰ نور هدیه) | مانند `users[0].password` | بخش ب (لغو جزئی ادمین با ۳ واحد `P1`، تفکیک سهم عادی/هدیه، گردکردن و برگشت ارسال در واحد آخر) |
| `cancelShippedBuyer` | از `buyers.items` (هدف ۱۰۰ نور عادی، ۰ هدیه) | مانند `users[0].password` | بخش ج (ممنوعیت لغو پس از `Shipped` و `Delivered` برای کاربر و ادمین) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `fixtures.physicalProductDocumentId` (`P1`)، `fixtures.basicCourseDocumentId` (`K1`)، `fixtures.couponCode` (`MANUALTEST10`).
- `users[key=admin|funded].externalUserId`.
- `buyers.items[]` برای نقش‌های `cancelBuyer`، `cancelPartialBuyer` و `cancelShippedBuyer` (`phone`، `externalUserId`، `internalUserId`، `targetBalanceNoor`، `targetGiftNoor`، `hasAddress`).

## قدم‌ها

### الف — لغو کاربر پیش از ارسال: سبد پرداخت‌نشده، سبد پرداخت‌شدهٔ مختلط با کوپن، تکرار و خطاها (`cancelBuyer`)

> موجودی اولیهٔ `cancelBuyer` را (`b0`) و موجودی فعلی `P1` در استرپی (`stock0`) را یادداشت کنید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `cancelBuyer` | (سبد بدون کالا) `POST /api/carts/checkout` با UUID تازهٔ `C_COURSE` و `{items:[{externalId: K1, type:"course", quantity: 1}]}` ← سپس `GET /api/me/physical-orders/{C_COURSE}/cancellation-preview` و `POST /api/me/physical-orders/{C_COURSE}/cancel` با `{operationId: UUID, reason:"تست بدون کالا"}` | هر دو `409 Conflict` با `code="PHYSICAL_CANCEL_CONFLICT"` |
| ۲ | Swagger با `cancelBuyer` | (سبد پرداخت‌نشده) `POST /api/carts/checkout` با UUID تازهٔ `C_UNPAID` و `{items:[{externalId: P1, type:"product", quantity: 1}]}` ← بررسی کاهش رزرو در استرپی ← سپس `GET /api/me/physical-orders/{C_UNPAID}/cancellation-preview` | `200`؛ `outcome="ReservationReleased"`، `items=[]`، `totalRefundInNoor=0`، `warnings=[]` |
| ۳ | Swagger با `cancelBuyer` | لغو سبد پرداخت‌نشده: `POST /api/me/physical-orders/{C_UNPAID}/cancel` با `{operationId: UUID, reason:"انصراف پیش از پرداخت"}` | `200`؛ `outcome="ReservationReleased"`، `actor="Buyer"`، `cancelledAtUtc=null`؛ موجودی آزادشدهٔ `P1` در استرپی به `stock0` برمی‌گردد؛ موجودی کیف پول تغییری نمی‌کند |
| ۴ | Swagger با `cancelBuyer` | (سبد در انتظار شارژ `AwaitingTopUp`) سبد تازهٔ `C_TOPUP` با ۱ عدد `P1` بسازید و با `POST /api/topup/requests` یک شارژ درگاه/کارت‌به‌کارت متصل به همان سبد باز کنید (یا `POST /api/carts/{C_TOPUP}/purchase` با کاربر بدون موجودی `09120000002` اگر سبد را به `AwaitingTopUp` ببرد) ← سپس `POST /api/me/physical-orders/{C_TOPUP}/cancel` | `409 Conflict` با `code="PHYSICAL_CANCEL_CONFLICT"` (رزرو در انتظار شارژ حدسی آزاد نمی‌شود) |
| ۵ | Swagger با `cancelBuyer` | (سبد مختلط پرداخت‌شده با کوپن) `POST /api/carts/checkout` با UUID تازهٔ `C_MIX` و `{items:[{externalId: K1, type:"course", quantity: 1},{externalId: P1, type:"product", quantity: 1}], couponCode:"MANUALTEST10"}` ← پرداخت با `POST /api/carts/{C_MIX}/purchase` | `200 Paid`؛ مبلغ پرداخت‌شده `18 (K1) + 30 (P1) + S = 48 + S`؛ موجودی کیف پول `b0 − (48 + S)`؛ موجودی `P1` در استرپی `stock0 − 1` |
| ۶ | Swagger با `cancelBuyer` | پیش‌نمایش لغو کاربر: `GET /api/me/physical-orders/{C_MIX}/cancellation-preview` | `200`؛ `outcome="Refunded"`، فقط قلم `P1` (`regularRefundInNoor=30, giftRefundInNoor=0`)، `shippingRegularRefundInNoor=S`، `totalRefundInNoor=30+S`، و در `warnings` کد `COUPON_REMOVED` هست (و `CARRIER_PAYMENT_PENDING` نیست) |
| ۷ | Swagger با `funded` (کاربر دیگر) | `GET /api/me/physical-orders/{C_MIX}/cancellation-preview` و `POST /api/me/physical-orders/{C_MIX}/cancel` با `{operationId: UUID, reason:"تست دسترسی"}` | هر دو `404 Not Found` با `code="PHYSICAL_CANCEL_NOT_FOUND"` |
| ۸ | Swagger با `cancelBuyer` | خطاهای اعتبارسنجی: `POST /api/me/physical-orders/{C_MIX}/cancel` با (الف) `reason:""`؛ (ب) `reason` ۵۰۱ نویسه‌ای؛ (ج) `operationId:"00000000-0000-0000-0000-000000000000"` | هر سه `400 Bad Request` |
| ۹ | Swagger با `cancelBuyer` | لغو موفق کل بخش فیزیکی سبد با `OP_B1` (یک UUID ثابت بسازید): `POST /api/me/physical-orders/{C_MIX}/cancel` با `{operationId: OP_B1, reason:"لغو کاربر پیش از ارسال"}` | `200`؛ `outcome="Refunded"`، `actor="Buyer"`، `totalRefundInNoor=30+S`؛ موجودی کیف پول به `b0 − 18` برمی‌گردد؛ موجودی `P1` در استرپی به `stock0` برمی‌گردد؛ مالکیت دورهٔ `K1` حفظ می‌شود و کوپن مصرف‌شده برنمی‌گردد |
| ۱۰ | Swagger با `cancelBuyer` | **تکرار دقیق** گام ۹ با همان `OP_B1` | `200` با همان پاسخ گام ۹؛ موجودی کیف پول (`b0 − 18`) و موجودی `P1` در استرپی (`stock0`) دوباره اضافه نمی‌شوند |
| ۱۱ | Swagger با `cancelBuyer` | **تعارض و تکرار با شناسهٔ تازه:** (الف) `POST /api/me/physical-orders/{C_MIX}/cancel` با یک `OP_B2` تازه؛ (ب) استفاده از همان `OP_B1` روی یک سبد فیزیکیِ دیگر | هر دو `409 Conflict` با `code="PHYSICAL_CANCEL_CONFLICT"` |

### ب — لغو جزئی ادمین: گردکردن باقیمانده برای آخرین واحد، تفکیک سهم عادی/هدیه و برگشت ارسال در تکمیل مرسوله (`cancelPartialBuyer` و `admin`)

> موجودی `cancelPartialBuyer` پیش از خرید: ۱۰۰ نور عادی و ۱۰ نور هدیه (`balance=110`، `giftBalance=10`). یک سبد `C_PART` با `3` عدد `P1` (جمع کالا `90` نور + ارسال `S`) بخرید و `purchaseId` قلم `P1` را از رسید بردارید (`PP1`). پیش از لغو، `GET /api/me/orders/cart:{C_PART}` را باز کنید و `regularAmountInNoor` و `giftAmountInNoor` آن را یادداشت کنید (`R_paid` و `G_paid`).

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `funded` (کاربر عادی) | `GET /api/admin/physical-purchases/{PP1}/cancellation-preview?quantity=1` و `POST /api/admin/physical-purchases/{PP1}/cancel` | هر دو `403 Forbidden` |
| ۲ | Swagger با `admin` | خطاهای اعتبارسنجی و تعارض: (الف) `GET .../PP1/cancellation-preview?quantity=0`؛ (ب) `POST .../PP1/cancel` با `quantity:0` یا `reason:""`؛ (ج) `GET .../PP1/cancellation-preview?quantity=4` (بیش از موجود)؛ (د) شناسهٔ ناموجود `11111111-1111-4111-8111-111111111111` | (الف) و (ب) `400 Bad Request`؛ (ج) `409 PHYSICAL_CANCEL_CONFLICT`؛ (د) `404 PHYSICAL_CANCEL_NOT_FOUND` |
| ۳ | Swagger با `admin` | پیش‌نمایش لغو `quantity=1` از ۳: `GET /api/admin/physical-purchases/{PP1}/cancellation-preview?quantity=1` | `200`؛ `shippingRegularRefundInNoor=0`، `shippingGiftRefundInNoor=0`، و در `warnings` کد `SHIPPING_NOT_REFUNDED` هست |
| ۴ | Swagger با `admin` | لغو واحد اول با `OP_A1`: `POST /api/admin/physical-purchases/{PP1}/cancel` با `{operationId: OP_A1, quantity: 1, reason:"لغو جزئی واحد اول"}` | `200`؛ `actor="Admin"`، `quantity=1`، سهم عادی و هدیه گرد به پایین (`floor(90*1/3)=30` کل، و سهم هدیه `floor(G_paid*1/3)` از آن)؛ ارسال `0`؛ موجودی کیف پول `cancelPartialBuyer` به تفکیک عادی/هدیه افزایش می‌یابد؛ موجودی `P1` در استرپی `+1` می‌شود |
| ۵ | Swagger با `admin` | **تکرار و تعارض `OP_A1`:** (الف) تکرار دقیق گام ۴ با `OP_A1` و `quantity:1`؛ (ب) ارسال همان `OP_A1` با `quantity:2` | (الف) `200` با همان پاسخ بدون برگشت/restock مجدد؛ (ب) `409 PHYSICAL_CANCEL_CONFLICT` |
| ۶ | Swagger با `admin` | لغو واحد دوم با `OP_A2`: `POST /api/admin/physical-purchases/{PP1}/cancel` با `{operationId: OP_A2, quantity: 1, reason:"لغو جزئی واحد دوم"}` | `200`؛ فقط مبلغ واحد دوم بدون ارسال برمی‌گردد؛ موجودی `P1` در استرپی `+1` می‌شود |
| ۷ | Swagger با `admin` | لغو واحد سوم (آخرین واحد) با `OP_A3`: `POST /api/admin/physical-purchases/{PP1}/cancel` با `{operationId: OP_A3, quantity: 1, reason:"لغو آخرین واحد و تکمیل مرسوله"}` | `200`؛ باقیماندهٔ گردکردن به همین واحد می‌رسد به‌طوری‌که جمع ۳ برگشت دقیقاً برابر `R_paid` (عادی) و `G_paid` (هدیه) است؛ چون مرسوله پیش از ارسال کامل بسته شد، هزینهٔ ارسال `S` هم برمی‌گردد؛ موجودی `P1` در استرپی به مقدار پیش از خرید برمی‌گردد |
| ۸ | Swagger با `cancelPartialBuyer` و `admin` | بررسی `GET /api/purchases`، `GET /api/me/orders/cart:{C_PART}/receipt` و `GET /api/admin/orders` | `cancelledQuantity=3`، `refundedRegularInNoor=R_paid`، `refundedGiftInNoor=G_paid` و در `GET /api/admin/orders` مقدار `refundedAmountInNoor = 90 + S` است؛ در `GET /api/wallet/balance` جمع عادی و هدیه دقیقاً به مقدار پیش از خرید برگشته است |

### ج — لغو پس از ارسال (`Shipped` و `Delivered`) و صف برگشت موجودی (`cancelShippedBuyer` و `admin`)

> یک سبد `C_SHIP` با ۱ عدد `P1` برای `cancelShippedBuyer` بخرید (`purchaseId = PS1`) و آن را با `PUT /api/admin/physical-purchases/{PS1}/delivery-status` با `{"status":"Shipped","trackingCode":"5555555555555","trackingProvider":"Manual"}` به وضعیت `Shipped` ببرید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `cancelShippedBuyer` | `GET /api/me/physical-orders/{C_SHIP}/cancellation-preview` و `POST /api/me/physical-orders/{C_SHIP}/cancel` با `{operationId: UUID, reason:"لغو پس از ارسال"}` | هر دو `409 Conflict` با `code="PHYSICAL_CANCEL_ALREADY_SHIPPED"` |
| ۲ | Swagger با `admin` | `GET /api/admin/physical-purchases/{PS1}/cancellation-preview?quantity=1` و `POST /api/admin/physical-purchases/{PS1}/cancel` با `{operationId: UUID, quantity: 1, reason:"لغو ادمین پس از ارسال"}` | هر دو `409 Conflict` با `code="PHYSICAL_CANCEL_ALREADY_SHIPPED"` |
| ۳ | Swagger با `cancelShippedBuyer` و `admin` | تحویل سفارش: `POST /api/me/orders/cart:{C_SHIP}/confirm-delivery` ← سپس تکرار گام ۲ با ادمین | `409 Conflict` با `code="PHYSICAL_CANCEL_ALREADY_SHIPPED"`؛ هیچ سطر لغو، برگشت نور یا restock ثبت نمی‌شود |
| ۴ | Swagger با `funded` و `admin` | بررسی صف برگشت موجودی: (الف) با `funded` روی `GET /api/admin/physical-purchases/pending-restocks`؛ (ب) با `admin` روی `GET /api/admin/physical-purchases/pending-restocks`؛ (ج) با `admin` روی `POST /api/admin/physical-purchases/restocks/{OP_B1}/retry` و `resolve` | (الف) `403 Forbidden`؛ (ب) `200 []` (چون restock عملیات‌های بخش الف و ب موفق بوده است)؛ (ج) `404 PHYSICAL_CANCEL_NOT_FOUND` (چون عملیات از قبل بسته شده و در صف ناتمام نیست) |

> **نکتهٔ آزمایش صف واقعی `pending-restocks` (اختیاری):** اگر بخواهید مسیر بازیابی دستی را زنده ببینید، پیش از یک لغو پرداخت‌شده استرپی را موقتاً متوقف کنید، لغو را بزنید (برگشت نور انجام و `200` برمی‌گردد)، در `GET /api/admin/physical-purchases/pending-restocks` عملیات را با `lastErrorCode` ببینید، استرپی را بالا بیاورید و با `POST /api/admin/physical-purchases/restocks/{operationId}/retry` آن را ببندید (یا پس از اصلاح دستی موجودی در استرپی، با `POST .../resolve` ببندید).

## نتیجهٔ موردانتظار

- لغو سبد پرداخت‌نشده فقط رزرو موجودی را در استرپی آزاد کند (`ReservationReleased`) و `Ledger`/کیف پول تغییری نکند؛ سبد `AwaitingTopUp` و سبد بدون کالا با `409 PHYSICAL_CANCEL_CONFLICT` رد شوند.
- لغو کاربر در سبد پرداخت‌شده کل بخش فیزیکی باقی‌مانده و ارسال را برگرداند، دسترسی دورهٔ همان سبد و مصرف کوپن را نگه دارد، و با `operationId` تکراری بدون برگشت/restock مضاعف همان پاسخ `200` را بدهد.
- لغو جزئی ادمین با گردکردن رو به پایین و انتقال باقیمانده به آخرین واحد، سهم عادی و هدیه را دقیقاً به اندازهٔ پرداخت واقعی برگرداند، ارسال را فقط هنگام تکمیل کل مرسوله پیش از ارسال برگرداند، و پس از `Shipped`/`Delivered` برای هر دو نقش با `409 PHYSICAL_CANCEL_ALREADY_SHIPPED` بسته باشد.
- برگشت موجودی استرپی (`restock`) پس از هر لغو پرداخت‌شده به‌طور خودکار و idempotent اعمال شود و در صورت شکست، در `pending-restocks` قابل پیگیری و بستن با `retry`/`resolve` باشد.

## ثبت نتیجه

- موفق: [گزارش شواهد](../Verification-Summary.md) — تاریخ، نسخهٔ بیلد، `cartId`/`purchaseId`/`operationId`، تراز عادی/هدیه پیش و پس از لغو، و موجودی استرپی.
- ناموفق: همان‌جا — کد خطا، پاسخ API، لاگ سرور.

## خارج از دامنه / نیازمند تأیید

- لغو سفارش دارای مرسولهٔ ساخته‌شده نزد پستکس در وضعیت «در انتظار پرداخت» (`CarrierPaymentPending` / `SHIP-FIX-1`) که در پیش‌نمایش ادمین هشدار `CARRIER_PAYMENT_PENDING` را نشان می‌دهد، به‌دلیل نیاز به کلید واقعی پستکس خارج از این سناریو است و اجرای زندهٔ آن نیازمند تأیید جداگانه است.
