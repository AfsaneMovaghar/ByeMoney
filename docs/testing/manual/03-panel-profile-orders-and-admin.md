# ۰۳ — پنل و پروفایل: فهرست و جزئیات سفارش، رسید، سفارش دستی ادمین، دسترسی کاربر غیرمجاز

> دامنه: فهرست/فیلتر و جزئیات سفارش کاربر و ادمین، رسید کل سفارش (پرداخت‌شده و پرداخت‌نشده)، پیش‌نمایش و ثبت سفارش دستی ادمین (پولی، رایگان صریح، کسری موجودی، کاربر ساخته‌شده در پنل، کاربر مسدود، تکرار درخواست)، و دسترسی کاربر غیرمجاز. قالب: [README](README.md).
> معیارها از I11-P، I11-T، I11-O و I11-AO در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، [ماتریس دسترسی](../../architecture/Permissions-Matrix.md)، D08 در [تصمیم‌ها](../../architecture/ByeMoney-Decisions.md) و ردیف‌های D1، D2 («پذیرش ثبت دستی سفارش ادمین») و «رسید ادمین و تغییر وضعیت تحویل» در [TASKS](../../../TASKS.md).

## پیش‌نیاز

- چهار سرویس بالا باشند (استرپی `:1337`، بای‌مانی `:5000`، فرانت `:3000`، PostgreSQL).
- `.\seed.ps1 -Action up` **با ادمین استرپی** اجرا شده باشد تا دادهٔ پایه (`C1`, `C2`, `CH1`, `CH2`, `P1`) و نقش `Admin` بای‌مانی به‌همراه نقش `administrator` استرپی برای کاربر ادمین (`09120000003`) ثبت شده باشند.
- `.\seed.ps1 -Action buyers` **یک بار پیش از اجرای این سناریو** اجرا شده و `.\seed.ps1 -Action verify` سبز باشد.
- اگر هنوز هیچ سفارش پرداخت‌شده (ترجیحاً دارای کالای فیزیکی `P1` برای آزمودن `shippingAddress` و `shippingOrigin`) یا سفارش پرداخت‌نشده در دیتابیس نیست، از سناریوی [۰۲ — سبد و خرید](02-cart-and-purchase.md) یک سفارش `Paid` (با `productBuyer` یا `courseBuyer`) و یک سبد `Ready` پرداخت‌نشده بسازید، یا از بخش ج همین سناریو برای ساخت سفارش `Paid` استفاده کنید.

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` بردارید، حدس نزنید):

| نماد | چیست | کلید در `fixtures` | مقدار ثابت |
|---|---|---|---|
| C1 | دورهٔ پایه، ۲۰ نور | `basicCourseDocumentId` | — |
| C2 | دورهٔ فصلی؛ قیمت پکیج ۰ | `seasonalCourseDocumentId` | — |
| CH1 / CH2 | فصل ۱ و ۲ دورهٔ فصلی، هرکدام ۱۰ نور | `seasonalChapter1IntegrationId` / `seasonalChapter2IntegrationId` | `a0000001-…c001` / `a0000002-…c002` |
| P1 | کالای فیزیکی، ۳۰ نور، موجودی ۱۰ | `physicalProductDocumentId` | — |

**قرارداد اجرا:**

- توکن هر کاربر از `POST http://localhost:1337/api/auth/password/login` گرفته و در Swagger با دکمهٔ **Authorize** (`Bearer <توکن>`) گذاشته می‌شود.
- برای هر سفارش دستی یک UUID تازه بسازید (`[guid]::NewGuid()`) و در تکرار همان درخواست دقیقاً همان UUID را بفرستید.
- پیش از بخش ج موجودی ذینفع را با `GET /api/wallet/balance` بخوانید و به‌عنوان `b0` یادداشت کنید.

## کاربر

| نقش | شماره / منبع | رمز | کاربرد در این سناریو |
|---|---|---|---|
| ادمین | `09120000003` (`users[key=admin]`) | `Manual-Test-2026` | بخش‌های ب، ج و د (دارای `Admin` در بای‌مانی و `administrator` در استرپی) |
| کاربر عادی دارای موجودی | `09120000001` (`users[key=funded]`) | `Manual-Test-2026` | بخش د (آزمون `403` در مسیرهای ادمین و `404` روی سفارش دیگران) |
| کاربر مسدود | `09120000004` (`users[key=blocked]`) | `Manual-Test-2026` | بخش ج-۲ (رد ذینفع مسدود با `409 CART_CONFLICT`) |
| `manualPaidBuyer` | از `buyers.items` (هدف ۱۰۰ نور) | مانند `users[0].password` | بخش‌های الف، ج-۱ و د (سفارش دستی پولی + پروفایل و رسید) |
| `manualShortfallBuyer` | از `buyers.items` (هدف ۰ نور) | مانند `users[0].password` | بخش ج-۲ (کسری موجودی و تکرار پس از شارژ) |
| `manualFreeBuyer` | از `buyers.items` (هدف ۰ نور) | مانند `users[0].password` | بخش ج-۳ (سفارش دستی رایگان با `isFree=true`) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `fixtures.basicCourseDocumentId` (`C1`)، `fixtures.seasonalCourseDocumentId` (`C2`)، `fixtures.seasonalChapter1IntegrationId` (`CH1`)، `fixtures.seasonalChapter2IntegrationId` (`CH2`)، `fixtures.physicalProductDocumentId` (`P1`).
- `users[key=admin|funded|blocked].externalUserId` و `internalUserId`.
- `buyers.items[]` برای نقش‌های `manualPaidBuyer`، `manualShortfallBuyer` و `manualFreeBuyer` (`externalUserId`، `phone`، `targetBalanceNoor`).

## قدم‌ها

### الف — پروفایل و فهرست، جزئیات و رسید سفارش کاربر (`manualPaidBuyer` یا خریدار دارای سفارش)

> اگر `manualPaidBuyer` هنوز سفارش پرداخت‌نشده ندارد، در گام ۱ یک سبد `Ready` با `C1` بسازید (`checkout` بدون `purchase`) تا وضعیت `AwaitingPayment` هم آزموده شود؛ و پس از بخش ج-۱ (یا با داشتن یک سفارش `Paid`) گام‌های سفارش پرداخت‌شده را ادامه دهید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن خریدار | `POST /api/carts/checkout` با `{cartId: U1, items:[{C1, course, 1}], couponCode:null}` (بدون `purchase`) | `200`؛ `status=Ready` |
| ۲ | Swagger با توکن خریدار | `GET /api/me/orders?page=1&pageSize=20&sourceType=Cart` | `200`؛ `{items, page:1, pageSize:20, totalCount}`؛ ردیف `orderId="cart:{U1}"` با `financialStatus="AwaitingPayment"`، `deliveryStatus="NotStarted"`، `itemCount=1`، بدون فیلدهای ادمین |
| ۳ | Swagger با توکن خریدار | `GET /api/me/orders/cart:{U1}` | `200`؛ جزئیات همان سبد پرداخت‌نشده برمی‌گردد (`cartStatus="Ready"`، `shippingAddress=null`، `reservationId=null`) |
| ۴ | Swagger با توکن خریدار | `GET /api/me/orders/cart:{U1}/receipt` | `404` با پیام «خرید پیدا نشد.» (فقط سفارش `Paid` رسید دارد) |
| ۵ | Swagger با توکن خریدار | تکرار `GET /api/me/orders` و `GET /api/me/orders/cart:{U1}` | پاسخ یکسان؛ `GET` هیچ تغییری در وضعیت، موجودی یا رکوردها نمی‌دهد |
| ۶ | Swagger با توکن خریدار | فیلتر نامعتبر: `GET /api/me/orders?financialStatus=BadStatus` یا `sourceType=Invalid` | `400` با خطای اعتبارسنجی |
| ۷ | Swagger با توکن دارندهٔ سفارش `Paid` (پس از بخش ج-۱ یا سفارش دارای کالای `P1`) | `GET /api/me/orders?financialStatus=Paid` ← سپس `GET /api/me/orders/{paidOrderId}` | در جزئیات سفارش `Paid`: `financialStatus="Paid"`، `reservationId` غیرتهی (برای سفارش `Cart`)؛ اگر سفارش کالا دارد `shippingAddress` (بدون فیلد `version`) و `shippingOrigin` پرند، و اقلام کالا `trackingCode`/`trackingProvider` دارند اما `providerParcelId` و `carrierPaymentPendingSinceUtc` **نیستند**؛ در هیچ حالتی `createdByAdminExternalUserId` و `adminFreeReason` نیستند |
| ۸ | Swagger با توکن همان خریدار | `GET /api/me/orders/{paidOrderId}/receipt` | `200`؛ `orderId`، `transactionId`، `cartSummary` (شامل `shippingAmountInNoor`) و `items[]` (شامل `refundedRegularInNoor`، `refundedGiftInNoor` و برای کالا `physicalDeliveryStatus`) بدون `providerParcelId`، `carrierPaymentPendingSinceUtc` و بدون `shippingAddress` |
| ۹ | مرورگر (`/profile/orders`) | ورود با همین خریدار ← صفحهٔ سفارش‌های من و باز کردن جزئیات و رسید | سفارش‌ها نمایش داده می‌شوند؛ جزئیات و رسید با ارقام بالا هم‌خوان‌اند |

### ب — پنل ادمین: فهرست، فیلترها، جزئیات و رسید (`admin` `09120000003`)

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `GET /api/admin/orders?page=1&pageSize=20` | `200`؛ هر ردیف علاوه بر فیلدهای پایه شامل `buyerExternalUserId`، `buyerDisplayName`، `topUpAttemptCount`، `refundedAmountInNoor`، `carrierReturnStatus` و `carrierAlertStatus` است |
| ۲ | Swagger با توکن ادمین | فیلترهای ترکیبی: `GET /api/admin/orders?buyerExternalUserId={manualPaidBuyer.externalUserId}&financialStatus=Paid` و همچنین `GET /api/admin/orders?awaitingShipment=true` | `totalCount` و `items` فقط مطابق همان فیلترها برمی‌گردند؛ `awaitingShipment=true` فقط سفارش‌های `Paid` دارای کالای فیزیکی لغونشده و ارسال‌نشده را می‌آورد |
| ۳ | Swagger با توکن ادمین | `GET /api/admin/orders/{unpaidOrderId}` (مثلاً `cart:{U1}`) | `200` با `{buyerExternalUserId, buyerDisplayName, order}` (برخلاف رسید، روی سفارش پرداخت‌نشده هم کار می‌کند) |
| ۴ | Swagger با توکن ادمین | `GET /api/admin/orders/{unpaidOrderId}/receipt` و نیز `GET /api/admin/orders/cart:11111111-1111-4111-8111-111111111111` | هر دو `404`؛ شناسهٔ با قالب نامعتبر یا `Guid.Empty` (`not-an-order` یا `cart:00000000-0000-0000-0000-000000000000`) `400` |
| ۵ | Swagger با توکن ادمین | `GET /api/admin/orders/{paidPhysicalOrderId}` و `GET /api/admin/orders/{paidPhysicalOrderId}/receipt` | در جزئیات ادمین: `shippingAddress` (شامل `version`)، `shippingOrigin` و در صورت ثبت مرسوله `providerParcelId` وجود دارند؛ در رسید ادمین نیز `providerParcelId` و `carrierPaymentPendingSinceUtc` (در صورت انتظار پرداخت) برمی‌گردند |
| ۶ | مرورگر (`/admin/orders`) | ورود با ادمین (`09120000003`) ← بخش سفارش‌ها، آزمودن فیلتر منبع (همه / قدیمی / بای‌مانی) و جست‌وجو با شماره/نام خریدار | ردیف‌های بای‌مانی اول و بدون دکمهٔ تغییر وضعیت مالی/حذف/گروهی (فقط‌مشاهده، جز عملیات تحویل کالا) نمایش داده می‌شوند |

### ج — سفارش دستی ادمین روی سبد (`I11-AO` / `D2`)

#### ج-۱: پیش‌نمایش، ثبت پولی، تکرار درخواست (`manualPaidBuyer`)

> موجودی `manualPaidBuyer` را پیش از شروع بخوانید و به‌عنوان `b0` یادداشت کنید (هدف ۱۰۰ نور). یک UUID تازه به‌نام `M1` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger استرپی با توکن ادمین | `POST /api/course-carts/admin-preview` با `{"beneficiaryExternalUserId":"{manualPaidBuyer.externalUserId}","items":[{"externalId":"{C1}","type":"course","quantity":1},{"externalId":"{CH1}","type":"course_chapter","quantity":1}]}` | `200`؛ `originalTotalInNoor=30`، `priceInNoor=30`؛ بدون رزرو و بدون تغییر موجودی یا رکورد سبد |
| ۲ | Swagger بای‌مانی با توکن ادمین | `POST /api/admin/carts/orders` با `{"cartId":"{M1}","beneficiaryExternalUserId":"{manualPaidBuyer.externalUserId}","items":[{"externalId":"{C1}","type":"course","quantity":1},{"externalId":"{CH1}","type":"course_chapter","quantity":1}],"isFree":false,"freeReason":null}` | `200` با `CartResponse` و `status="Paid"`؛ `priceInNoor=30` |
| ۳ | Swagger با توکن `manualPaidBuyer` / استرپی CM | `GET /api/wallet/balance`، `GET /api/wallet/transactions` و بررسی `api::order.order` در استرپی | موجودی `b0 − 30`؛ یک ردیف `Purchase` با `amountNoor=30`؛ **هیچ رکورد `order` در استرپی ساخته نشده است** |
| ۴ | CM یا مرورگر با `manualPaidBuyer` | بررسی مالکیت در استرپی / صفحهٔ دورهٔ فصلی `C2` | `C1` و **فقط فصل ۱ (`CH1`)** باز هستند؛ کل دورهٔ `C2` و فصل ۲ (`CH2`) باز نشده‌اند |
| ۵ | Swagger بای‌مانی با توکن ادمین | **تکرار دقیق** گام ۲ با همان `cartId: M1` و همان بدنه | `200` با همان `CartResponse` (`status="Paid"`)؛ موجودی `manualPaidBuyer` دیگر کم نمی‌شود (`b0 − 30`) |
| ۶ | Swagger بای‌مانی با توکن ادمین | **تکرار متعارض** با همان `cartId: M1` ولی قلم یا `isFree`/`freeReason` متفاوت | `409` با `{code:"CART_CONFLICT"}` |
| ۷ | Swagger بای‌مانی با توکن ادمین / خریدار | `GET /api/admin/orders/cart:{M1}` در برابر `GET /api/me/orders/cart:{M1}` و `GET /api/admin/orders/cart:{M1}/receipt` | در جزئیات ادمین: `createdByAdminExternalUserId` برابر `externalUserId` ادمین و `adminFreeReason=null`؛ در `/api/me/orders/...` و رسیدها این دو فیلد **وجود ندارند** |

#### ج-۲: خطاهای سفارش دستی، کسری موجودی و تکرار پس از شارژ (`manualShortfallBuyer` و `blocked`)

> موجودی `manualShortfallBuyer` برابر `0` است. یک UUID تازه به‌نام `M2` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بای‌مانی با توکن ادمین | `POST /api/admin/carts/orders` برای `manualShortfallBuyer` با `cartId: M2`، `items: [{C1, course, 1}, {CH1, course_chapter, 1}]` و `isFree: false` | `409` با `{code:"INSUFFICIENT_NOOR_BALANCE"}`؛ هیچ اثر مالی ثبت نمی‌شود و رزرو استرپی آزاد است |
| ۲ | Swagger بای‌مانی با توکن خریدار و ادمین | شارژ `manualShortfallBuyer` به مبلغ `30` نور (`POST /api/topup/requests` + `POST /api/admin/topups/{id}/confirm`) ← سپس **تکرار دقیق گام ۱ با همان `cartId: M2`** | تکرار موفق: `200` و `status="Paid"`؛ موجودی `0` می‌شود |
| ۳ | Swagger بای‌مانی با توکن ادمین | قلم قبلاً خریده‌شده: `cartId` تازه با `{C1, course, 1}` برای `manualPaidBuyer` | `409` با `{code:"CART_CONFLICT"}` |
| ۴ | Swagger بای‌مانی با توکن ادمین | قلم غیرمجاز (کالای فیزیکی): `cartId` تازه با `[{"externalId":"{P1}","type":"product","quantity":1}]` | `400` با شکل خطای اعتبارسنجی پروژه |
| ۵ | Swagger بای‌مانی با توکن ادمین | خطای ترکیب `isFree` و `freeReason`: (الف) `isFree=true` با `freeReason=null` یا خالی؛ (ب) `isFree=false` با `freeReason="test"` | هر دو `400` با خطای اعتبارسنجی |
| ۶ | Swagger بای‌مانی با توکن ادمین | ذینفع مسدود: `cartId` تازه با `beneficiaryExternalUserId="{blocked.externalUserId}"` | `409` با `{code:"CART_CONFLICT"}` |
| ۷ | Swagger بای‌مانی با توکن ادمین | ذینفع ناموجود: `cartId` تازه با `beneficiaryExternalUserId="nonexistent-user-id-000"` | `404` با شکل عمومی خطای پروژه |

#### ج-۳: سفارش دستی رایگان صریح (`manualFreeBuyer`) و کاربر ساخته‌شده در پنل ادمین

> موجودی `manualFreeBuyer` برابر `0` است. یک UUID تازه به‌نام `M3` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بای‌مانی با توکن ادمین | `POST /api/admin/carts/orders` برای `manualFreeBuyer` با `cartId: M3`، `items: [{CH1, course_chapter, 1}]`، `isFree: true` و `freeReason: "بورسیه آزمون دستی"` | `200` با `status="Paid"`، `originalTotalInNoor=10`، `priceInNoor=0` |
| ۲ | Swagger بای‌مانی و استرپی | `GET /api/wallet/balance` و `GET /api/wallet/transactions` برای `manualFreeBuyer` + بررسی دسترسی در استرپی | موجودی همچنان `0`؛ هیچ ردیف Ledger ساخته نشده؛ فقط `CH1` برای `manualFreeBuyer` باز است و `C2`/`CH2` باز نیستند |
| ۳ | Swagger بای‌مانی با توکن ادمین و خریدار | `GET /api/admin/orders/cart:{M3}` در برابر `GET /api/me/orders/cart:{M3}` و `GET /api/me/orders/cart:{M3}/receipt` | در جزئیات ادمین: `createdByAdminExternalUserId` پر و `adminFreeReason="بورسیه آزمون دستی"`؛ در `/api/me/orders/...` و رسید: `finalAmountInNoor=0` و بدون `createdByAdminExternalUserId`/`adminFreeReason` |
| ۴ | مرورگر (`/admin` ← ثبت کاربر جدید و ثبت سفارش دستی) | ورود با ادمین (`09120000003`) ← ساخت یک کاربر تازه با شمارهٔ آزاد از فرم پنل (که `POST /api/admin/users` را با `confirmed: true` صدا می‌زند) ← ثبت سفارش دستی رایگان (`CH1`) برای همین کاربر در فرم پنل | سفارش با موفقیت ثبت می‌شود، در `/admin/orders` دیده می‌شود و با ورود آن کاربر در `/profile/orders` و صفحهٔ دوره قابل مشاهده است |

### د — دسترسی کاربر غیرمجاز

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بدون توکن (پاک کردن Authorize) | `GET /api/me/orders`، `GET /api/admin/orders`، `POST /api/admin/carts/orders` | همه `401` |
| ۲ | Swagger بای‌مانی با توکن کاربر عادی (`09120000001`) | `GET /api/admin/orders`، `GET /api/admin/orders/cart:{M1}`، `GET /api/admin/orders/cart:{M1}/receipt`، `POST /api/admin/carts/orders` | همه `403` (کاربر عادی مجوز `Purchases.View` و `Courses.Manage` ندارد) |
| ۳ | Swagger استرپی با توکن کاربر عادی (`09120000001`) | `POST /api/course-carts/admin-preview` با بدنهٔ معتبر | `403` |
| ۴ | Swagger بای‌مانی با توکن کاربر عادی (`09120000001`) | `GET /api/me/orders/cart:{M1}` و `GET /api/me/orders/cart:{M1}/receipt` (متعلق به `manualPaidBuyer`) | هر دو `404` (عدم افشای وجود سفارش کاربر دیگر) |
| ۵ | مرورگر / درخواست با نشست کاربر عادی لاگین‌شده (`09120000001`) | فراخوانی routeهای سروری Next.js: (الف) `POST /api/admin/orders/manual` و `GET /api/admin/byemoney-orders/cart:{M1}/receipt`؛ (ب) `GET /api/admin/byemoney-orders`، `GET /api/admin/orders/manual`، `POST /api/admin/users` | در (الف) `403` برمی‌گردد؛ در (ب) **اختلاف کد و سند**: کد فعلی به‌جای `403` پاسخ `401` برمی‌گرداند (بنابر بررسی کد در `byemoney-orders/route.js`، `orders/manual/route.js` و `users/route.js`) |

## نتیجهٔ موردانتظار

- کاربر در پروفایل فقط سفارش‌ها و رسیدهای خودش را می‌بیند، سفارش دیگران `404` است و سفارش پرداخت‌نشده رسید ندارد (`404`) اما جزئیاتش باز است (`200`).
- ادمین دارای مجوز (`Purchases.View` / `Courses.Manage`) فهرست، فیلترها، جزئیات کامل (شامل `shippingAddress` با `version`، `providerParcelId` و اطلاعات ثبت ادمین) و رسید ادمین را می‌بیند.
- سفارش دستی ادمین فقط در بای‌مانی (`Cart` + `CoursePurchase`) ثبت می‌شود و هیچ رکورد `order` در استرپی نمی‌سازد؛ در حالت رایگان هیچ Ledgerی ثبت نمی‌شود و در تکرار دقیق با همان `cartId` عملیات تکرارپذیر (`idempotent`) است.
- کاربر بدون توکن (`401`) یا فاقد مجوز (`403`) به مسیرهای ادمین بای‌مانی و پیش‌نمایش استرپی دسترسی ندارد.

## ثبت نتیجه

- موفق: بخش‌های «پذیرش ثبت دستی سفارش ادمین» و «رسید ادمین و تغییر وضعیت تحویل» در [گزارش شواهد](../Verification-Summary.md) — تاریخ، نسخهٔ بیلد، `orderId`ها و `cartId`های استفاده‌شده.
- ناموفق: همان بخش — کد پاسخ، بدنهٔ پاسخ، و آخرین خط `logs/log-*.txt` بای‌مانی یا لاگ کنسول استرپی/فرانت.

## خارج از دامنه / نیازمند تأیید

- سفارش دستی کالای فیزیکی: با تصمیم مالک خارج از دامنهٔ `I11-AO` است و عمداً با `400` رد می‌شود.
- ساخت مرسولهٔ واقعی پستکس و فراخوانی درگاه پرداخت (SEP) و ارسال پیامک در این سناریو انجام نمی‌شوند.
