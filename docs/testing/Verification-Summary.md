# خلاصهٔ شواهد آزمون و پذیرش

## انتقال پیام خطای واقعی `prepare` استرپی در تعارض سبد کالا (`AUDIT-CART-02` / `UI-001`) — ۲۷ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney`):**
  - در `CartPricingClient.FailureAsync` هنگام دریافت پاسخ `409 Conflict` از استرپی، پس از بررسی خطای موجودی و کمبود اطلاعات مرسوله، برای سایر تعارض‌ها نیز پیام واقعی `error` استرپی (از جمله خطاهای `CART_SHIPPING_UNAVAILABLE` و تعارض مبدأ) به‌همراه `lineError` معتبر (در صورت وجود) به پاسخ `409 CART_CONFLICT` منتقل می‌شود و تنها در نبود پیام معتبر به پیام عمومی تعارض برمی‌گردد.
  - افزودن آزمون‌های واحد در `CartPricingClientTests` برای تعارض بدون `lineError` (مانند خطای ارسال پستکس و تداخل مبدأ)، تعارض دارای `lineError` غیرموجودی، و بازگشت ایمن به پیام عمومی در بدنهٔ خالی یا نامعتبر.
- **نتیجه:**
  - اجرای هدفمند `CartPricingClientTests` با پیکربندی `Release`: **۴۳/۴۳ سبز** (`Failed: 0, Skipped: 0`). *(اجرای کامل مجموعهٔ آزمون‌ها به‌دلیل اجرای هم‌زمان آزمون‌ها در نشست دیگر متوقف شد.)*

## همگام‌سازی وضعیت مسدودسازی کاربر استرپی و رد متمرکز با `403` (`AUDIT-AUTH-01` / `LOG-010`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney`، شاخهٔ `fix/sync-blocked-user-status`):**
  - **تأمین و همگام‌سازی کاربر (`UserProvisioningService.EnsureUserFromExternalAsync`):** برای کاربران موجود نیز وضعیت فعلی از استرپی خوانده و `IsActive = confirmed && !blocked` (بدون دخالت `isMobileVerified`) با زمان `DateTime.UtcNow` به‌روزرسانی می‌شود؛ در صورت قطع یا خطای استرپی، به‌جای اتکا به دادهٔ کهنه، خطای `503` برگردانده می‌شود (fail-closed).
  - **رد متمرکز با `403` (`RbacClaimsTransformation` + `ActiveUserMiddleware`):** ادعای `is_active` در تبدیل نقش‌ها ثبت می‌شود (و در صورت غیرفعال‌بودن، نقش‌ها/مجوزها افزوده نمی‌شوند) و میان‌افزار `ActiveUserMiddleware` پس از `UseAuthentication` هر درخواست احراز هویت‌شدهٔ کاربر غیرفعال/مسدود را روی مسیرهای `[Authorize]` با کد `403` رد می‌کند، بدون اثر بر مسیرهای `AllowAnonymous` یا احراز هویت `X-Service-Key`.
  - **یکدست‌سازی مسیرهای مالی/ادمین:** افزودن `ResultStatus.Forbidden` به `Result`/`Result<T>`؛ در `AdminAssistedTopUpService` و `CartService.PrepareCoreAsync` (سفارش دستی ادمین) ذی‌نفع مسدود/غیرفعال با `403` رد می‌شود و در اهدای دسته‌ای هدیه (`AdminGrantGiftNoorCommandHandler`) خطای مسدودبودن برای همان ذی‌نفع برگردانده می‌شود.
- **نتیجه:**
  - `ByeMoney.UnitTests` با پیکربندی `Release`: **۸۵۱/۸۵۱ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*
  - پوشش آزمون‌های افزوده‌شده/به‌روزشده: همگام‌سازی وضعیت کاربر موجود (فعال ↔ مسدود)، رفتار fail-closed هنگام قطع استرپی، رد ذی‌نفع مسدود در شارژ به‌نیابت و سفارش دستی با `403`، حذف مجوزها و پاسخ `403` میان‌افزار برای توکن کاربر غیرفعال، و عبوردادن مسیرهای `AllowAnonymous`/بدون توکن.

## بستن دسترسی عمومی و کاربر عادی به سفارش‌های قدیمی و فهرست کوپن در استرپی (`AUDIT-SEC-02` / `LOG-008`، `LOG-009`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (`backend`، شاخهٔ `byeMoney`):**
  - **`LOG-008` (سفارش‌ها):** افزودن policy `global::is-admin-or-api-token` به همهٔ مسیرهای هستهٔ سفارش (`find`، `findOne`، `create`، `update`، `delete`) در `src/api/order/routes/order.js` و افزودن گارد `canManageOrders` به `find` و `findOne` در `src/api/order/controllers/order.js`؛ مسیر پروکسی Next.js (`front-end/src/app/api/orders/route.js`) و `ordersApi.js` با `STRAPI_API_TOKEN` (`api-token`) متصل می‌مانند.
  - **`LOG-009` (کوپن‌ها):** افزودن policy `global::is-admin-or-api-token` به مسیرهای هستهٔ کوپن (`find`، `findOne`، `create`، `update`، `delete`) در `src/api/coupon/routes/coupon.js` با حفظ `auth: false` و محدودیت نرخ روی `POST /api/coupons/validate` در `src/api/coupon/routes/01-custom-coupon.js`.
  - **پاک‌سازی مجوزهای دیتابیس استرپی:** افزودن `revokeSensitivePublicPermissions` در `src/services/securityPermissions.js` و فراخوانی در `bootstrap` (`src/index.js`) برای حذف مجوزهای حساس سفارش و کوپن از نقش‌های `public` و `authenticated` در `plugin::users-permissions.permission`.
- **نتیجه:**
  - آزمون‌های واحد `tests/api/order/orderAndCouponPublicAccess.test.js`: **۱۷/۱۷ سبز**.
  - مجموعهٔ کامل آزمون‌های `backend` (`npm test`): Jest (**۶۲ سوییت، ۴۷۰/۴۷۰ سبز**) و `node --test` (**۱۰۴/۱۰۴ سبز**، ۰ شکست).
- **محدودیت:** آزمون‌ها با شبیه‌سازی Strapi انجام شدند؛ در اجرای زندهٔ استرپی هنگام بالا آمدن `bootstrap` مجوزهای دیتابیس پاک می‌شوند و درخواست‌های بدون توکن/کاربر عادی باید `401`/`403` بگیرند.

## ماتریس عملیات مدیریت سفارش بای‌مانی (`TASK-ADMIN-ORDER-OPS-REVIEW`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (تحلیلی-مستنداتی):** بررسی و تطبیق عملیات پنل ادمین سفارش‌ها با کنترلرهای `AdminOrdersController`، `AdminPhysicalPurchasesController`، `AdminPhysicalCancellationsController` و `AdminOrderSettlementsController` و تصمیم مالک:
  - عملیات معتبر و موجود: مشاهده/جستجو/جزئیات/رسید، تغییر وضعیت ارسال کالا، لغو کالای فیزیکی پیش از ارسال، و تسویهٔ گروهی.
  - عملیات نامعتبر روی سفارش بای‌مانی: تغییر دستی وضعیت سفارش/پرداخت، تأیید/رد رسید کارت‌به‌کارت روی سفارش، و حذف تکی/گروهی.
  - تصمیم مالک: خروجی اکسل سفارش بای‌مانی در فاز اول نیاز نیست؛ سفارش‌های قدیمی استرپی (از جمله پرداخت‌نشده‌های قدیمی) در فهرست جداگانهٔ Legacy می‌مانند.
  - نکتهٔ هم‌ترازی مجوزها (`RequireNoorInject` روی لغو کالا) پیش‌تر در `RBAC-05` (۱۰ نوامبر ۲۰۲۶) در اسناد و ماتریس مجوزها ثبت شده بود.
- **نتیجه:** ماتریس در `D10` سند `ByeMoney-Decisions.md` ثبت و ردیف `TASK-ADMIN-ORDER-OPS-REVIEW` از `TASKS.md` بسته و حذف شد. (بدون تغییر کد.)

## رفع ناهم‌خوانی ترتیب `purchaseIds` و `restock` در سبد مختلط (`AUDIT-CART-01` / `LOG-006`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney` + `backend`، شاخهٔ `fix/mixed-cart-restock-order`):**
  - **`ByeMoney` (`CartService.SynchronizeAsync`):** نگاشت صریح `ProductItemType` به کلید سیمی استرپی (`course`، `course_chapter`، `product`) و مرتب‌سازی `purchaseIds` با `StringComparer.Ordinal` بر اساس `(canonicalType, ExternalItemId)` در زمان `commit` تا با ترتیب `physicalCartQuote.selection` در استرپی یکسان باشد؛ ترتیب `Cart.Items` و تخصیص مالی دست‌نخورده ماند.
  - **آزمون‌ها:** افزودن `MixedCartSynchronizeOrderTests` در بای‌مانی و `tests/services/physicalCartMixedRestock.test.js` در استرپی برای سبد مختلط با `product.externalId < chapter.externalId < course.externalId` (پوشش `commit`، لغو جزئی/کامل، `restock`، تلاش مجدد idempotent با `operationId` یکسان و رد برگشت بیش از تعداد خرید).
- **نتیجه:**
  - `ByeMoney.UnitTests` با پیکربندی `Release`: **۸۴۱/۸۴۱ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*
  - `backend` Jest (`tests/api tests/services tests/config`): **۶۱ سوییت، ۴۵۳/۴۵۳ سبز**؛ و `node --test`: **۹۶/۹۶ سبز** (`0` شکست).
- **محدودیت:** آزمون استرپی با شبیه‌ساز تراکنش درون‌حافظه انجام شد؛ در محیط واقعی با نشست زنده نیز بررسی شود.

## اصلاح چرخهٔ شارژ درگاه سپ و بازیابی آن (`AUDIT-GW-01` / `LOG-002` تا `LOG-005`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (هر سه پروژه، شاخهٔ `fix/gateway-topup-recovery`):**
  - **`LOG-002` (`ByeMoney` + `front-end`):** در `src/app/api/payment/request/route.js` اگر تلاش دارای توکن صادرشده یا در جریان باشد، شکست دریافت توکن جدید دیگر رویداد `Unpaid` نمی‌فرستد و وضعیت تلاش را خراب نمی‌کند. در `GatewayTopUpService.RecordResultAsync` هنگام دریافت `Verified` روی شارژ `Rejected` (با حفظ گاردهای `ManualRefundReference` و `ReverseSucceeded`)، شواهد بانکی (`HasVerifiedHistory`، `ExternalTransactionId`، `GatewayResultCode`) و پروندهٔ `BANK_CONFLICT` در دیتابیس ذخیره شده و سپس پاسخ تعارض `409` (`TOPUP_REQUIRES_REVIEW`) بدون واریز خودکار نور برگردانده می‌شود.
  - **`LOG-003` (`front-end` + `backend`):** در `src/lib/processGatewayTopUpCallback.js` هر callback دارای `RefNum` حتی خارج از `token_issued` یا شکست قفل `claimAttempt` هم verify می‌شود (و در صورت رد قبلی با نبود بازپرداخت دستی، مسیر برگشت ایمن را می‌رود). در کنترلر `gateway-payment-attempt` استرپی، رویدادهای callback تا تحویل واقعی به بای‌مانی در وضعیت `pending` می‌مانند (`deliveryStatus` فقط برای `reverse_intent` به‌صورت `delivered` ساخته می‌شود).
  - **`LOG-004` (`front-end` + `ByeMoney`):** در `processGatewayTopUpCallback.js` کد `۲` سپ پس از تطبیق مشخصات و مبلغ با snapshot به‌عنوان `Verified` ثبت و ارسال می‌شود تا یک‌بار تسویه شود یا سابقهٔ `HasVerifiedHistory` برای شارژ استثنایی ثبت بماند.
  - **`LOG-005` (`backend`):** در `src/services/gatewayTopUpRecovery.js` در شاخهٔ خطای `409`، اگر وضعیت فعلی تلاش `confirmed` یا `reversed` باشد، به `financial_review` برگردانده نمی‌شود.
- **نتیجه:**
  - `ByeMoney.UnitTests` با پیکربندی `Release` (شامل آزمون‌های Postgres و `GatewayResultRecordingTests`): **۸۴۰/۸۴۰ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*
  - `backend` (`npm --prefix backend test`): Jest کامل (**۴۰۴+ تست سبز**) و `node --test` (**۹۶/۹۶ سبز**، ۰ شکست).
  - `front-end`: Jest مسیرهای پرداخت (`request/route.test.js`، `verify/route.test.js`، `verify/latePayment.test.js`، `gatewayTopUp.test.js`): **۴۵/۴۵ سبز** (`Failed: 0`).
- **محدودیت:** آزمون‌ها با شبیه‌سازی درگاه سپ انجام شدند؛ پذیرش با درگاه واقعی سپ در محیط مقصد در بخش «نیازمند تست دستی» باز می‌ماند.

## بستن مسیرهای بدون احراز سفارش قدیمی و مسدودکردن اعطای دسترسی از سفارش تازه در استرپی (`AUDIT-SEC-01` / `LOG-001` و `AUDIT-LEGACY-01` / `LOG-007`) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (`backend`، شاخهٔ `byeMoney`):**
  - **`AUDIT-SEC-01` (`LOG-001` / `HF18`، `M6`):** حذف `auth: false` از مسیرهای `export-excel`، `revenue-stats`، `sync-excel` و `bulk-settle` در `src/api/order/routes/01-custom-order.js` و اعمال `canManageOrders` (`api-token` یا نقش `administrator`) در چهار عمل کنترلر `src/api/order/controllers/order.js`؛ انتقال مسیر پیش‌فرض خروجی `generateAndSaveExcelReport` در `src/api/order/services/excel-export.js` از `public/uploads/exports` به `storage/exports` (خارج از `public/`).
  - **`AUDIT-LEGACY-01` (`LOG-007`):** مسدودکردن ثبت سفارش جدید دارای `order.course-order-item` (یا `courseId`/`chapterId`/`chapterIntegrationId`) یا وضعیت پرداخت‌شده (`paid`، `confirmed`، `processing`، `shipped`، `delivered` / `paymentStatus=paid`) در `order.create` کنترلر سفارش حتی با `api-token` یا نقش `administrator`؛ حذف فراخوانی `syncUserPurchases` از `afterCreate` در `src/api/order/content-types/order/lifecycles.js` تا ساخت مستقیم سفارش در Content-Manager هم دسترسی دوره یا فصل ندهد.
  - آزمون‌های واحد در `tests/api/order/orderSecurityAndLegacyGuard.test.js` و به‌روزرسانی آزمون `afterCreate` در `tests/order-price-hotfix.test.js`.
- **نتیجه:** اجرای کامل `npm --prefix backend test`: Jest (`51` سوییت، **۴۰۲/۴۰۲ سبز**) و `node --test` (**۹۶/۹۶ سبز**، ۰ شکست).
- **محدودیت:** آزمون در برابر داده‌های شبیه‌سازی‌شده انجام شد؛ روی سرور استرپی زنده پس از استقرار بررسی شود.

## پذیرش دستی مرورگر و پنل (محلی / بدون پستکس زنده و SEP) — ۲۶ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney` + `backend` + `front-end` روی localhost):** پذیرش دستی بخش «الف» از [TASKS](../../TASKS.md) با نشست واقعی مرورگر در `http://localhost:3000`، `costMode=fixed` (۵ نور، ارسال رایگان بالای ۱۰۰ نور) و کاربران تست (`09121000001`، `09120000001`، `09120000002`، `09120000003`، `09120000004`):
  - **پروفایل، پنل ادمین و سفارش دستی (`D2` و `I11-O`):** بررسی دیداری `/profile/orders`، `/admin/orders`، نمایش `shippingAddress` روی سفارش‌های کالایی و ساخت کاربر جدید در فرم پنل ادمین.
  - **خرید دوره، فصل، سبد مختلط و نتیجهٔ خرید (`R01` / `سناریوی ۰۲` و `FE-ORDER-02`):** بررسی دیداری «دوره‌های من»، صفحهٔ دورهٔ فصلی (فصل ۱ خریداری‌شده و فصل ۲ قابل خرید)، جریان سبد در `/cart` (انتخاب/تغییر آدرس، سبد جایگزین) و صفحهٔ `/checkout/result` برای خرید فقط‌کالا و مختلط با کیف پول.
  - **پذیرش `SHIP-FIX-2` در مرورگر:** نمایش نشان برگشتی، متن تأیید جزئی، لینک امن رهگیری و کنترل هشدار پستی (`11304`) در پروفایل کاربر و کارت مدیر.
  - **شارژ ادمین و لغو فیزیکی (`C8`):** ثبت شارژ از فرم پنل ادمین و آزمون سرتاسری لغو فیزیکی/جزئی و برگشت نور و موجودی در فرانت.
- **نتیجه:** همهٔ ۶ مورد بخش «الف» (پذیرش مرورگر و پنل) توسط کاربر در مرورگر تأیید شدند و از بخش «نیازمند تست دستی» [TASKS](../../TASKS.md) بسته شدند.
- **محدودیت / باقی‌مانده:** بخش‌های «ب» (پستکس زنده و Content-Manager) و «پ» (درگاه SEP، توکن/CSP و محیط مقصد) در این دور تست نشده‌اند و همچنان در [TASKS](../../TASKS.md) باز می‌مانند.

## آماده‌سازی استقرار بای‌مانی روی لیارا (`DEPLOY-01` گام ۱–۲) — ۲۵ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney`):**
  - ساخت `liara.json` در ریشهٔ Solution با `platform: dotnet`، `dotnet.version: 10.0`، پورت ۵۰۰۰ و دو دیسک با مسیر نسبی (`receipts/` برای رسیدهای خصوصی و `logs/` برای لاگ). طبق مستندات لیارا (۲۰۲۶-۱۱-۲۵)، پوشهٔ Solution پشتیبانی می‌شود و liara.json باید در همان پوشه باشد؛ دیسک .NET مسیر نسبی می‌گیرد. پلتفرم آمادهٔ dotnet نسخهٔ ۱۰.۰ را فهرست کرده است؛ Dockerfile لازم نشد.
  - انتقال `Cors:AllowedOrigins` و `TarhElahi:BaseUrl` از `appsettings.json` پایه به `appsettings.Development.json`، تا در محیط مقصد originهای localhost پیش‌فرض نیایند و CORS فقط با env پروداکشن تعیین شود. مقدار env آرایه اندیس‌به‌اندیس override می‌کند؛ اکنون پایه هیچ origin ندارند و مقدار خالی به `AllowAnyOrigin` نمی‌رسد (در پروداکشن باید env ست شود).
- **نتیجه:** `dotnet build ByeMoney.sln -c Release`: ۰ خطا، ۰ هشدار. `ByeMoney.UnitTests` با پیکربندی `Release`: **۸۳۷/۸۳۷ سبز** (`Failed: 0, Skipped: 0`).
- **محدودیت / باقی‌مانده:** ساخت اپ و PostgreSQL در پنل لیارا، تنظیم env پروداکشن (اسرار، connection string، CORS، `TarhElahi:BaseUrl`)، استقرار واقعی، health check و توکن واقعی، به تصمیم انتشار مالک وابسته است و در بخش «نیازمند تست دستی / P3» باقی می‌ماند. آزمون `docker build` انجام نشد، چون Dockerfile لازم نشد.

## نگاشت کد `409` در پروکسی تغییر وضعیت تحویل فرانت (`I11-O`) — ۲۵ نوامبر ۲۰۲۶

- **دامنه (`front-end`):** افزودن شاخهٔ `409` (`conflict: true`) به `updatePhysicalPurchaseDeliveryStatus` در `byeMoneyApi.js`، نگاشت `result.conflict` به کد `409` در `PUT /api/admin/physical-purchases/[purchaseId]/delivery-status`، و پوشش `409` در شرط پیام انتقال غیرمجاز `PhysicalDeliveryControl.jsx`.
- **نتیجه:** Jest محلی فرانت (`3` سوییت: `byeMoneyPhysicalOrders.test.js`، `delivery-status/route.test.js`، `PhysicalDeliveryControl.test.jsx`): **۵۹/۵۹ سبز**.
- **محدودیت:** آزمون واحد با `fetch` شبیه‌سازی‌شده؛ پذیرش با مرورگر واقعی در بخش «نیازمند تست دستی» پیگیری می‌شود.

## هم‌ترازی تکرار `evidenceId` در بازگشایی رسیدگی مالی درگاه (`I07`) — ۲۵ نوامبر ۲۰۲۶

- **دامنه (`ByeMoney`):**
  - در `TopUpRequest.ReopenFinancialReview` تکرار `evidenceId` پیش از بررسی وضعیت پرونده بررسی می‌شود؛ تکرار فقط وقتی بی‌اثر (`200`) است که رکورد قبلی در `ReviewAudit` از نوع `"evidence"` در همین پرونده و با `Note` دقیقاً برابر باشد (بدون مقایسهٔ زمان سرور). تغییر `note` یا استفاده از شناسهٔ متعلق به عمل دیگر، تعارض `409` با کد `REVIEW_CASE_CONFLICT` برمی‌گرداند.
  - در `GatewayTopUpService.ReopenReviewAsync` تکرار بی‌اثر ذخیرهٔ اضافه انجام نمی‌دهد و در `DbUpdateConcurrencyException` رکورد تازه در scope جدید خوانده شده و با همان قاعدهٔ دامنه بررسی می‌شود.
  - آزمون‌های واحد در `ManualGatewayReviewTests` (تکرار یکسان، `note` متفاوت، شناسهٔ عمل دیگر، تکرار پس از بسته شدن دوباره، و شبیه‌سازی `DbUpdateConcurrencyException`) و ثبت قاعده در بخش `I07` [قرارداد اتصال](../integration/ByeMoney-Strapi-Integration.md).
- **نتیجه:** `dotnet build ByeMoney.sln -c Release` (۰ خطا، ۰ هشدار) و `ByeMoney.UnitTests` با پیکربندی `Release`: **۸۳۷/۸۳۷ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*

## هم‌ترازی کد خطا و اعتبارسنجی کد رهگیری در `I11-O` — ۲۵ نوامبر ۲۰۲۶

- **دامنه (شاخهٔ `fix/delivery-status-contract`):**
  - افزودن متادیتای `[ProducesResponseType(StatusCodes.Status409Conflict)]` به `AdminPhysicalPurchasesController.UpdateDeliveryStatus` و هم‌ترازی بخش `I11-O` در [قرارداد اتصال](../integration/ByeMoney-Strapi-Integration.md) برای کدهای `409` (تعارض وضعیت فعلی)، `400` (خطای ورودی/کد رهگیری)، `404` و `200` بی‌اثر.
  - سخت‌گیرتر کردن `PhysicalProductPurchase.IsUsablePostalTrackingCode` برای رد هر کد رهگیری دارای `*` (ماسک کامل یا نیمه‌ماسک) در همهٔ مسیرهای ثبت دستی، اصلاح کد، ساخت مرسوله، بررسی وضعیت و همگام‌سازی خودکار.
  - افزودن آزمون‌های کد نیمه‌ماسک در `ShipTrackingTests`، `CreatePhysicalShipmentTests` و `PhysicalDeliveryHttpPostgresTests`.
- **نتیجه:** `ByeMoney.UnitTests` (شامل آزمون‌های واحد و یکپارچهٔ HTTP/PostgreSQL تحویل کالا) با پیکربندی `Release`: **۸۲۸/۸۲۸ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*
- **محدودیت / کار باقی‌مانده:** در فرانت‌اند (`front-end`) تغییری داده نشد؛ پروکسی سروری Next.js برای `delivery-status` پیام خطای `409` را به ادمین می‌رساند ولی شاخهٔ صریح `409` ندارد و کد HTTP را `500` برمی‌گرداند (ثبت‌شده در `P2` [TASKS](../../TASKS.md)).

## بازبینی هم‌ترازی قراردادها، `I07` و `I09-CH` — ۲۵ نوامبر ۲۰۲۶

- دامنه: بازبینی اجرای ۴ پرامپت هم‌ترازی و پاک‌سازی در بای‌مانی و فرانت‌اند:
  - **`I09-CH` (کامیت `a138977`):** اصلاح پیش‌چک مالکیت فصل‌ها در `CartStore.cs` و `CoursePurchaseRepository.cs` (`ExternalParentId` دیگر خرید کل دوره شمرده نمی‌شود، در حالی که خرید کل دوره همچنان خرید فصل و خرید تکراری همان قلم را با `409 CART_CONFLICT` رد می‌کند) به‌همراه آزمون‌های `CartPostgresTests` و `DuplicatePurchasePreventionTests`.
  - **`I07` (تغییرات کاری بای‌مانی):** نگاشت `TopUpRequest_ReviewStateMismatch` به `REVIEW_STATE_MISMATCH` در `GatewayTopUpService.ResolveManualReviewAsync` و هم‌ترازی `ProducesResponseType` اکشن `GatewayTopUpController.ReopenReview` به `GatewayReviewState` به‌همراه `ManualGatewayReviewTests`.
  - **مستندات (`ByeMoney-Strapi-Integration.md`، `ByeMoney-Decisions.md`، `Postex-API.md`):** ثبت `CARRIER_PAYMENT_PENDING` در `I09-PC`، `UNPAID_REJECTED` و شواهد `PAID_AND_CONFIRMED` در `I07`، کدهای `NOOR_WITHDRAWAL_RATE_UNAVAILABLE` و `NOOR_WITHDRAWAL_INVALID_RECEIPT` و وضعیت `NOOR_WITHDRAWAL_AMOUNT_TOO_SMALL` در `I11-W`، شرح اثر انگشت `I11-S`، تطبیق تعریف سفارش رایگان در `D18` و ارجاع والد `7` برای کد `11228`.
  - **فرانت‌اند (کامیت `4f579b3`):** اصلاح `403` و پذیرش `administrator`/`admin` در مسیرهای سروری ادمین Next.js (شاهد Jest زیر)؛ برچسب `SettlementModal` و پاک‌سازی کلاس‌های `OrdersTable.module.scss` پیش‌تر انجام شده بودند.
- **نتیجه:**
  - `ByeMoney.UnitTests` با پیکربندی `Release` (`--no-restore`): **۸۲۱/۸۲۱ سبز** (`Failed: 0, Skipped: 0`). *(اجرای `Debug` به‌خاطر قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` انجام نشد.)*
  - Jest مسیرهای ادمین فرانت (`6` سوییت، شامل `byemoney-orders`، `orders/manual`، `orders/manual/preview`، `users`، `noor-gifts`، `noor-withdrawals`): **۷۲/۷۲ سبز**.
- **محدودیت:** پذیرش دیداری مرورگر سناریوهای ۰۲ و ۰۳ و موارد تصمیم‌محور باقی‌مانده در `P1` (`I11-O`، مقایسهٔ `Reason`/`operationId` در `I09-PC`، مقایسهٔ `note` در `ReopenFinancialReview` و کد خطای رسید `I11-W`) همچنان در [TASKS](../../TASKS.md) بازند.

## مسیرهای سروری ادمین Next.js — وضعیت ۴۰۳ و نقش admin — ۲۵ نوامبر ۲۰۲۶

- دامنه: دروازهٔ `requireAdminJwt` و مسیرهای `GET /api/admin/byemoney-orders`، `GET/POST /api/admin/orders/manual` (به‌همراه پیش‌نمایش)، `GET/POST /api/admin/users`. نقش با `isUserAdmin` در `auth.js` یکسان است (`administrator` و `admin`). برچسب مودال تسویه و سه کلاس CSS نام‌برده در P4 هم بررسی شد.
- **نتیجه:** Jest محلی فرانت، هر ۱۲ سوییت `src/app/api/admin`، ۱۱۴ آزمون سبز. بدون نشست `401`، کاربر لاگین‌شدهٔ غیرادمین `403`، و هر دو نقش `administrator` و `admin` پذیرفته می‌شوند. برچسب `SettlementModal` از قبل «عنوان دورهٔ تسویه» بود. کلاس‌های `action_btn--receipt`، `settlementPanel--open` و `timeline--final` در `front-end/src` نبودند؛ قاعده‌های نزدیک و در حال استفاده (`settlementPanel__chevron--open` و `timeline__item--final`) دست نخوردند.
- **محدودیت:** پذیرش دیداری مرورگر سناریوی ۰۳ همچنان در بخش «نیازمند تست دستی» است. این اجرا فقط Jest فرانت بود.

## سبد و خرید — اجرای بخش‌های کالایی (ه، و، ح) با پستکس زنده — ۲۵ نوامبر ۲۰۲۶

- دامنه: بخش‌های `ه`، `و` و `ح` از [سناریوی ۰۲](manual/02-cart-and-purchase.md)، سه سرویس روی localhost، JWT واقعی استرپی، دستهٔ خریداران `09120000057`–`…67` (`seed.ps1 -Action buyers` با آدرس کامل تبریز). `costMode=postex`، `collectionType=postex_drop_off`، `courierCode=IR_POST`، `serviceType=EXPRESS`، `rialPerNoorForShipping=10`، مبدأ `ramsar` (`573`). قیمت `P1` و `P2` موقتاً برای عبور از کف ارزش اعلامی پستکس به `12,000` نور (`120,000` ریال) تنظیم و پس از آزمون به `30` و `5` برگردانده شد؛ سه خریدار کالایی (`09120000061`، `09120000062`، `09120000064`) با درخواست شارژ و تأیید ادمین `500,000` نور شارژ شدند.
- **نتیجه:** همهٔ گام‌های API بخش‌های `ه`، `و` و `ح` (به‌جز نگاه مرورگر `و#۵`) موفق بودند:
  - **کالا با تعداد (`ه`، `09120000061`):** سبد `ee44f4e3-cba6-41a4-8e03-bea901792967` با `P1×2` ← `200 Ready`، برآورد زندهٔ پستکس `157,850` نور (`برآورد پستکس`)، کل `181,850` نور، `shippingOrigin.id=ramsar`، کاهش موجودی `P1: 10→8` هنگام checkout؛ پس از `purchase` موجودی کیف پول `500,100→318,250` و `P1` بدون کاهش دوم (`8`)؛ سفارش در `GET /api/me/orders` با `181,850`؛ خرید مجدد `P1` مجاز (`200 Ready`)؛ چهار خطای اعتبارسنجی (`quantity=0`، جفت تکراری، اقلام خالی، `cartId` تهی) همه `400`.
  - **سبد مختلط (`و`، `09120000062`):** سبد `4d3e72d6-e3e3-4f3c-862a-9ae2bbad6502` با `C1` + `P1×1` + کوپن `MANUALTEST10` ← `originalTotalInNoor=12,020`، `couponDiscountInNoor=2` (فقط روی دوره)، ارسال `157,850`، کل `169,868`؛ پس از `purchase` مانده `500,100→330,232`، `P1: 8→7`، `C1=Completed`؛ رسید `GET /api/me/orders/cart:4d3e72d6-…/receipt` با `200` و ۲ قلم؛ خرید دوبارهٔ همان ترکیب به‌خاطر مالکیت `C1` با `409 CART_CONFLICT` رد شد و موجودی `P1` (`7`) دست نخورد.
  - **کسری موجودی کالا (`ح`، `09120000064` و `09120000061`):** درخواست `P2×2` با موجودی `1` ← `409 CART_STOCK_UNAVAILABLE` (`INSUFFICIENT_STOCK`، `availableQuantity=1`) و سبد `Failed` و موجودی `P2=1`؛ سبد مختلط دارای `P2×2` کل خرید را با همین `409` متوقف کرد (`P1=7` دست‌نخورده)؛ خرید `P2×1` روی سبد `6d90b704-f4ef-4ef6-b3fb-ee1a362d3c10` ← `Paid` (`169,850` نور، `P2: 1→0`)؛ درخواست بعدی `P2×1` از `09120000061` ← `409 CART_STOCK_UNAVAILABLE` (`OUT_OF_STOCK`، `availableQuantity=0`)؛ اعمال کوپن فقط‌دوره روی سبد فقط‌کالا ← `400 CART_INVALID`. در پایان `P2` با لغو ادمین (`5241926a-…`) با `outcome=Refunded` به موجودی `1` برگشت.
- **نکتهٔ تثبیت‌شده:** علت خطای قبلی پستکس («هیچ سرویس دهندهٔ پستی امکان ارسال این مرسوله را ندارد») کف ارزش اعلامی پستکس بود: با `rialPerNoorForShipping=10` مبالغ `50` تا `10,000` ریال رد و `120,000` ریال (`12,000` نور) به بالا پذیرفته می‌شوند (`1,496,000` ریال).
- **محدودیت:** سه نگاه مرورگر سناریوی ۰۲ (`الف#۱۱`، `د#۵`، `و#۵`) باز نشدند.

## پنل، پروفایل، رسید و سفارش دستی ادمین — اجرای کامل API سناریوی ۰۳ — ۲۵ نوامبر ۲۰۲۶

- دامنه: [سناریوی ۰۳](manual/03-panel-profile-orders-and-admin.md)، سه سرویس محلی (`localhost:1337`, `localhost:5000`, `localhost:3000`)، JWT واقعی استرپی، دستهٔ خریداران `09120000046`–`…56` (`seed.ps1 -Action buyers`) و ادمین `09120000003` (با نقش `Admin` بای‌مانی و `administrator` استرپی). بدون نشست مرورگر، پستکس و SEP.
- **نتیجه (۳۴/۳۴ موفق):**
  - **پروفایل و سفارش کاربر (`الف`):** سبد پرداخت‌نشده `U1=7092dac5-…` در `GET /api/me/orders` با `AwaitingPayment` و در `GET /api/me/orders/{id}` با `200`، رسیدش `404`، تکرار `GET` بدون اثر، فیلتر نامعتبر `400`، جزئیات و رسید سفارش `Paid` (`M1=4114f6e7-…`) با `200` و بدون فیلدهای ادمین.
  - **پنل ادمین (`ب`):** فهرست ادمین با فیلترهای `buyerExternalUserId`+`financialStatus=Paid` و `awaitingShipment=true`، جزئیات سفارش پرداخت‌نشده `200`، رسید پرداخت‌نشده و ناموجود `404`، شناسهٔ نامعتبر و `Guid.Empty` برابر `400`، رسید پرداخت‌شده `200`.
  - **سفارش دستی ادمین (`ج`):** پیش‌نمایش استرپی `admin-preview` با `200` (`price=30`)؛ ثبت پولی `M1` (`C1` + `CH1`) با کسر `100→70` و باز شدن فقط `CH1` (`C2=NotPurchased`)؛ تکرار دقیق `M1` با `200` بدون برداشت دوم و تکرار متعارض `409 CART_CONFLICT`؛ کسری `M2=0112cfa7-…` با `409 INSUFFICIENT_NOOR_BALANCE` و موفقیت تکرار پس از شارژ ۳۰ نوری (`200 Paid`، مانده `0`)؛ قلم قبلاً خریده‌شده و ذینفع مسدود `409 CART_CONFLICT`، کالا و ترکیب نامعتبر `isFree`/`freeReason` با `400`، ذینفع ناموجود `404`؛ سفارش رایگان صریح `M3=3b4564aa-…` (`isFree=true`) با `200 Paid`، مانده `0`، بدون ردیف گردش و ثبت `adminFreeReason` فقط در جزئیات ادمین.
  - **دسترسی غیرمجاز (`د`):** بدون توکن `401`، کاربر عادی (`09120000001`) روی مسیرهای ادمین بای‌مانی و `admin-preview` استرپی `403`، و روی سفارش کاربر دیگر `404`؛ مسیرهای سروری Next.js بدون نشست `401` برگرداندند.
- **اختلاف‌های ثبت‌شده در [TASKS](../../TASKS.md) (در زمان این اجرا کد تغییر نکرد):** (۱) مسیر `cart:00000000-0000-0000-0000-000000000000` (`Guid.Empty`) به‌عنوان ورودی نامعتبر `400` می‌گیرد (متن سناریو اصلاح شد)؛ (۲) اختلاف ۴۰۱/۴۰۳ و نقش `admin` در مسیرهای سروری Next.js، همان روز در فرانت اصلاح شد (شاهد «مسیرهای سروری ادمین Next.js» بالای همین فایل).
- **محدودیت / باقی‌مانده:** بررسی دیداری مرورگر (`/profile/orders`، `/admin/orders` و ساخت کاربر جدید در فرم پنل) و `shippingAddress` روی یک سفارش دارای کالا با نشست مرورگر انجام نشد و در بخش «نیازمند تست دستی» `TASKS.md` باقی می‌ماند.

بازبینی و فشرده‌سازی: ۲۰۲۶-۱۱-۰۷. این گزارش فقط نتیجه و محدودیت پذیرش را نگه می‌دارد. شمارش آزمون‌های اجراهای مختلف با هم جمع نمی‌شود. آزمون محلی، ادغام و انتشار سه شاهد مستقل‌اند. کار باقی‌مانده فقط در [TASKS](../../TASKS.md) و [HOTFIXES](../../HOTFIXES.md) است. نام آزمون‌ها، SHA و شرح اجرا در Git است. ثبت تازه فقط تاریخ، نسخه، محیط، دامنه، نتیجه و محدودیت داشته باشد.

## سبد و خرید — اجرای کامل بخش‌های غیرکالایی سناریوی ۰۲ — ۲۵ نوامبر ۲۰۲۶

- دامنه: [سناریوی ۰۲](manual/02-cart-and-purchase.md)، سه سرویس روی localhost، JWT واقعی استرپی، دستهٔ خریداران `09120000027`–`…34` (ساختهٔ `seed.ps1 -Action buyers` با آدرس). ادمین استرپی در مسیر محتومدیر (خواندن موجودی کالا، `usedCount` کوپن، تنظیمات ارسال) استفاده شد. بدون SEP؛ پستکس فقط در حد یک استعلام برآورد (بخش کالا) صدا زده شد.
- **نتیجه:** همهٔ گام‌های بخش‌های دوره، کوپن (تخفیف، پیش‌نمایش بدون مصرف، سقف مصرف و آزادسازی)، فصل، سفارش رایگان، کسری کیف پول و شارژ، دسترسی و تکرار تأیید شد. نمونهٔ اعداد: دوره ۱۰۰←۸۰ با یک ردیف `Purchase` منفی ۲۰؛ کوپن ۲۰←۱۸ با `usedCount` یکی بیشتر و برگشت آن پس از لغو؛ فصل ۱ ده نور و فصل ۲ ده نور و «کل دوره پس از یک فصل» ۴۰۹؛ دورهٔ رایگان با موجودی صفر `Paid` و بدون ردیف گردش؛ کسری ۲۰ نور و ۲۰۰ ریال با `400 CART_INSUFFICIENT_BALANCE`، سپس تأیید ادمین روی شارژ درگاهی `۴۰۹` و رد `۲۰۴` و بازگشایی همان سبد؛ هم‌زمانی `purchase` و `checkout` یک برداشت و یک رزرو.
- **نکات تثبیت‌شده:** مبلغ ردیف گردش خرید منفی است (بدهی)؛ تکرار و هم‌زمانی `purchase` یک برداشت می‌سازد؛ پیش‌نمایش (`/api/course-carts/preview`) کوپن را مصرف نمی‌کند؛ سقف کوپن در سبد دوم `۴۰۰ CART_INVALID` می‌دهد و با لغو سبد اول آزاد می‌شود؛ سبد Failed با همان `cartId` بازگشایی و خرید می‌شود؛ لغو سبد Paid بدون اثر است؛ تأیید ادمین روی شارژ درگاهی `۴۰۹ «روش پرداخت نامعتبر است»` و رد آن `۲۰۴` است و پس از رد، همان سبد دوباره Ready می‌شود.
- **اختلاف کد و سند (تکرار شاهد):** خرید یکجای دورهٔ فصلی پس از خرید یک فصل `۴۰۹` داد (I09-CH مجاز می‌داند). همچنین `GET /api/courses/{chapterId}/checkout` برای فصلِ بدون ردیف خرید `۴۰۴` می‌دهد؛ پس «فقط همان فصل باز شد» را باید از رکورد کاربر دید.
- **مانع بیرونی:** با `costMode=postex` استعلام پستکس برای مرسولهٔ مبدأ رامسر با «هیچ سرویس دهندهٔ پستی امکان ارسال این مرسوله را ندارد» رد شد (دو شهر مقصد و سه روش جمع‌آوری امتحان شد)؛ بخش‌های کالا، سبد مختلط و کسری موجودی کالا اجرا نشدند. تنظیمات ارسال و آدرس به حالت قبل برگشت.

## سبد و خرید — اجرای هنگام نگارش سناریوی ۰۲ — ۲۵ نوامبر ۲۰۲۶

- دامنه: [سناریوی ۰۲](manual/02-cart-and-purchase.md)، سه سرویس روی localhost، JWT واقعی استرپی، خریداران تازهٔ `seed.ps1 -Action buyers` (شماره‌های `09120000019`–`…26`). بدون SEP، پستکس و مرورگر.
- **اجرا و تطبیق شد (HTTP):** دوره: `Ready` → `Paid`/`Completed`، برداشت ۲۰ یک‌بار، تکرار `purchase` همان پاسخ، خرید دوباره `409 CART_CONFLICT`. فصل: خرید CH1 ده نور و `Completed`؛ خرید دوبارهٔ فصل `409`. سفارش رایگان: دورهٔ فصلی با قیمت صفر و موجودی صفر `Paid`، بدون ردیف گردش. کوپن: پیش‌نمایش ۱۸ نور، `usedCount` با checkout یک بیشتر و با commit دو بار نشد؛ کد نامعتبر `400 CART_INVALID`. کسری: `purchase` → `400 CART_INSUFFICIENT_BALANCE` و سبد `Ready` ماند؛ `topup` → `AwaitingTopUp/Pending`، تکرار همان شارژ؛ تأیید ادمین روی شارژ درگاهی `409`، رد `204`، checkout دوباره همان سبد `Ready`، شارژ مستقل + خرید `Paid`. هم‌زمانی: دو `purchase` و دو `checkout` هم‌زمان ← یک برداشت، یک سبد. دسترسی: بدون توکن `401`، سبد کاربر دیگر در چهار مسیر `404`، لغو تکراری بی‌اثر، سبد لغوشده با checkout دوباره `Ready`، لغو سبد `Paid` بدون تغییر. اعتبارسنجی: `400` برای تعداد دوره ۲، جفت تکراری، اقلام خالی و `cartId` تهی.
- **اختلاف کد و سند:** خرید یکجای دورهٔ فصلی پس از خرید یک فصل `409` داد، برخلاف I09-CH (ثبت در TASKS). `isFree` در مسیر سبد خوانده نمی‌شود (D18).
- **اجرا نشد:** کالا با تعداد، سبد مختلط، کسری موجودی کالا، ظرفیت کوپن تک‌نفره و سه نگاه مرورگر — خریداران بدون آدرس بودند (بدون ادمین استرپی). این اجرا شاهد پذیرش آن گام‌ها نیست.

## پذیرش دستی محلی (بدون SEP و بدون شارژ پنل پستکس) — ۲۴ نوامبر ۲۰۲۶

- دامنه: سه سرویس روی localhost (بای‌مانی ۵۰۰۰، استرپی ۱۳۳۷، فرانت ۳۰۰۰). JWT کاربر/ادمین با مسیر واقعی ثبت‌نام/ورود استرپی (`node .tmp/acc/*`؛ دادهٔ تست ۴۴۰۱–۴۴۰۳، ۴۵۰۱–۴۵۱۰). پستکس زنده برای ساخت/شارژ و SEP واقعی عمداً اجرا نشد.
- **شارژ ادمین (API):** `POST /api/admin/topups/assisted` با multipart و کلید تکرار — با رسید → `Confirmed` و یک ردیف Ledger؛ بدون رسید همان؛ تکرار همان کلید → همان پاسخ بدون Ledger اضافه؛ کلید با مبلغ متفاوت → `409`؛ بدون کلید → `۴۰۰`؛ کاربر بدون نقش → `۴۰۳`. شاهد: `wallet-d2-r06.steps2.json`.
- **SHIP-FIX-2 (HTTP):** تک‌قلم برگشتی → `confirm-delivery` برابر `۴۰۹` و بدون `Delivered` در DB؛ مختلط (یک برگشتی + یک سالم) → `۲۰۰` و `confirmedCount=1`؛ `providerParcelId` در جزئیات/رسید ادمین هست و در پاسخ خریدار نیست (جستجوی مقدار خام)؛ نقش ادمین روی PUT تحویل `۴۰۳`. شاهد: `shipfix2-raw.json`، `shipfix2-part3.json`. سناریوی یکپارچهٔ قدیمی `s2-step6.js` به‌خاطر تغییر دادهٔ تکراری قابل اتکا نیست؛ مرجع همان `shipfix2.js`.
- **I11-O / تحویل ادمین:** همان اجرا مسیرهای `PUT delivery-status` (Preparing→Shipped→Delivered، قفل پس از Delivered با `۴۰۹`/`۴۰۰`)، `GET` سفارش ادمین/خریدار و RBAC را پوشش داد (توکن استرپی، نه مرورگر).
- **D2 سفارش دستی ادمین (HTTP، ناقص):** دورهٔ پولی و فصل پولی کاربر موجود → `۲۰۰`/`Paid`؛ لیست `GET /api/admin/orders` سفارش‌ها را نشان می‌دهد؛ فهرست `orders` استرپی برای همان کاربر همچنان ۰. کاربر مسدود، کاربر تازه و دورهٔ رایگان تکراری به‌خاطر دورهٔ از قبل خریده‌شده یا دادهٔ قدیمی `۴۰۹` گرفتند، نه رد صریح «blocked» — برای بستن ردیف باید با کاربر/دورهٔ تازه تکرار شود.
- **R06:** `purchase` هم‌زمان روی یک سبد → یک موفق؛ خرید مجدد همان دوره → `۴۰۹` در prepare. مسیر Legacy و staging اجرا نشد.
- **FE-ORDER-02:** Jest `src/app/checkout/result` — ۳۱ موفق. صفحهٔ نتیجه با سفارش واقعی فقط‌کالا در مرورگر باز نشد.
- **بای‌مانی (بازاجرای محلی):** فیلتر `PhysicalDeliveryHttpPostgresTests|CarrierReturnResolutionTests` — ۲۳ موفق (بدون rebuild به‌خاطر قفل API).
- **SHIP-FIX-3:** اثرانگشت پستکس در لاگ بای‌مانی و استرپی محلی یکسان (`12ef3ec4…`). پروفایل نمونه در ارسال زنده، Content-Manager و استقرار مقصد انجام نشد.
- **اجرا نشد (ماند در TASKS):** S3-02 زنده، SEP، شارژ پستکس، C8 سرتاسری فرانت، R01 کامل، R03 callback بانک، BND-08 CSP یک‌ساعته، TASK-ADMIN-GATEWAY-ATTEMPTS-READ با توکن سایت، SHIP-MULTI-ORIGIN در CM، مرورگر پروفایل/ادمین برای برگشتی و FE-ORDER-02، DEPLOY.

## پذیرش آزمون‌های pg استرپی — ۲۴ نوامبر ۲۰۲۶

- دامنه: فقط اجرای آزمون، بدون تغییر کد. `npm run test:pg` در `backend/` روی PostgreSQL 17 محلی با `ORDER_PRICE_TEST_DB` و `PHYSICAL_STOCK_PG_TESTS=1`: چهار فایل (`order-price-hotfix.pg`، `pending-checkout.pg`، `physicalStock.pg.cjs`، `lateSuccessPayment.pg.cjs`)، ۲۴ موفق، ۰ شکست، ۰ ردشده. هر فایل در schema موقت مستقل اجرا و پاک شد؛ دادهٔ `tarhelahi_db` دست نخورد. `npm test` در همان اجرا: jest ۵۰ مجموعه و ۳۷۱ موفق، `test:node` ۹۶ موفق. ردیف TASKS بسته شد.
- اجرای دوبارهٔ بای‌مانی روی master محلی (Windows، SDK 10.0.400، `dotnet build` بدون خطا): `dotnet test` با `BYEMONEY_CART_PG_TESTS=1` و `BYEMONEY_TEST_CONNECTION_STRING` روی `ByeMoney_Dev` در schema موقت — ۸۱۶ موفق، ۰ شکست، ۰ ردشده. اجرای اول فقط با یک کلید، ۴۷ آزمون را رد کرده بود که با کلید دوم تکرار شد.
- محدودیت: این دو اجرا ردیف‌های دیگر بخش «نیازمند تست دستی» را نمی‌بندد؛ معیار باقی‌ماندهٔ آن‌ها محیط مقصد، بانک، پستکس زنده، نشست مرورگر واقعی یا تأیید مالک است و در این ماشین انجام نشد.

## SHIP-FIX-4 — S1-08 و S1-05 — ۲۴ نوامبر ۲۰۲۶

- دامنه: استرپی و بای‌مانی. فرانت، دفترکل، کیف پول و مهاجرت بای‌مانی عوض نشدند. دو فیلد `courierCode` و `serviceType` به تنظیم ارسال استرپی اضافه شد؛ استعلام و پاسخ `collection-type` از همان تنظیم می‌خوانند. ساخت مرسوله کوریر و سرویس را فقط از همین پاسخ می‌گیرد و `added_service` را با سه پرچم `false` می‌فرستد. نبود جعبه یا وزن همان ۴۰۹ و `CART_CONFLICT` است و `lineError` قلم را دارد. `ALREADY_OWNED` و `SALE_STOPPED` همچنان `CART_CONFLICT` کلی می‌مانند. پستکس زنده صدا زده نشد.
- نتیجه: Jest در `backend/` برای `shippingCollection`، `shippingCollectionType`، `byeMoneySettingsService`، `cartShipping`، `postexShippingQuote`، `physicalCartQuote`، `physicalCartPrepare` و `shippingCmLabels` — ۸ مجموعه، ۷۹ موفق از ۷۹. `dotnet test` فیلتر `ShippingCollectionClientTests` و `PostexShipmentCityCodeTests` و `CreatePhysicalShipmentTests` و `CartPricingClientTests` — ۹۵ موفق از ۹۵. محیط ویندوز.
- محدودیت: ستون‌های تازهٔ استرپی تا بالا آمدن بعدی Strapi روی پایگاه اعمال نشده‌اند. فرم Content-Manager و مرورگر باز نشد. S3-02 با کلید واقعی مانده است. فرانت `lineError` را فقط برای `CART_STOCK_UNAVAILABLE` می‌خواند؛ برای این ۴۰۹ متن `error` را نشان می‌دهد.

## SCOPE-01 — محدودهٔ مالی طرح الهی — ۲۴ نوامبر ۲۰۲۶

- دامنه: فقط بای‌مانی، روی شاخهٔ جاری. فرانت و استرپی عوض نشدند. مهاجرت `20261124101851_AddFinancialScope`. ستون‌های scope و فروشنده الزامی‌اند؛ مقدار موقت `TARHE_ELAHI` فقط ردیف‌های موجود را پر کرد و بعد پیش‌فرض برداشته شد. ایندکس یکتای خرید تکراری دست نخورد.
- نتیجه (بازبینی مستقل، ۲۰۲۶-۱۱-۲۴): کل مجموعه با هر دو کلید PostgreSQL (`BYEMONEY_CART_PG_TESTS=1` و `BYEMONEY_TEST_CONNECTION_STRING`) روی `ByeMoney_Dev` و در schema موقت اجرا شد: ۸۰۸ آزمون، ۸۰۸ موفق، ۰ شکست، ۰ ردشده. اجرای اول ایجنت فقط بخش بدون پایگاه را زده بود (۶۸۵ موفق) و چهار آزمون قید پایگاه را که با ستون اجباری `ScopeId` شکسته بودند ندیده بود؛ insertهای خام آن فایل scopeدار شد. دو آزمون قید scope اضافه شد (رد مقدار ناشناخته، یکتایی حساب سیستم در هر scope نه به‌صورت سراسری) و آزمون برگشت لغو به کیف همان scope روی پایگاه واقعی اضافه شد (`PhysicalCancellationPostgresTests`). مقدار مورد انتظار آزمون PostgreSQL هم‌زمانی هدیهٔ خوش‌آمد که از کامیت `0d4f808` با مبلغ کمپین بذری (۵۰۰۰۰) ناهم‌تراز مانده بود، هم‌تراز شد. مهاجرت `AddFinancialScope`، برگشت به `AddCartShippingOriginCityId` و اعمال دوباره، هر سه روی PostgreSQL توسعه بدون خطا (گزارش ایجنت اجراکننده؛ بازبین تکرار نکرد).
- محدودیت: صفحهٔ مدیریت در مرورگر بررسی نشد.

## SHIP-FIX-4 — S1-01 — ۲۳ نوامبر ۲۰۲۶

- دامنه: استرپی، بای‌مانی و فرم ساخت مرسوله در پنل. تنظیم `collectionType` روی `shipping-setting` با پیش‌فرض `courier_drop_off`. استعلام و ساخت هر دو همین مقدار را می‌خوانند. انتخاب روش از فرم جزئیات سفارش و از بدنهٔ ساخت برداشته شد. دکمهٔ «ارسال به پستکس» و انتخاب جعبه ماندند. `lat`/`lon` فقط برای `pick_up` فرستاده می‌شود. Ledger، کیف پول و مهاجرت بای‌مانی عوض نشدند.
- نتیجه: `dotnet test` فیلتر `CreatePhysicalShipmentTests` و `PostexShipmentCityCodeTests` و `ShippingCollectionClientTests` با `-c Release --no-restore` — ۵۳ موفق از ۵۳. محیط ویندوز. Jest استرپی برای `shippingCollection` و `cartShipping` و `postexShippingQuote` و `byeMoneySettingsService` و `shippingCmLabels` — ۴۹ موفق از ۴۹. Jest فرم و route سبد — ۲۶ موفق از ۲۶. مقدار تهی به پیش‌فرض مصوب برمی‌گردد؛ `pick_up` و `postex_drop_off` از تنظیم خوانده می‌شوند؛ قطع خواندن تنظیم، ساخت را پیش از واسط متوقف می‌کند.
- محدودیت: ریستور NuGet به‌خاطر تایم‌اوت دانلود Swashbuckle انجام نشد و خروجی Debug را فرایند در حال اجرای `ByeMoney.API` قفل کرده بود؛ شاهد بالا با Release و بدون ریستور است. فرم در Content-Manager و مرورگر با نشست ادمین باز نشد. شناسهٔ دفتر برای `postex_drop_off` فاز بعد است. S1-05 و S1-08 و S3-02 بازند.
- پیگیری بازبینی همان روز: دکمهٔ «پیشبرد به تحویل‌شده» فقط اقلام `Shipped` را می‌فرستد و با شکست یکی متوقف نمی‌شود؛ توضیح فیلد، غیرقابل‌استفاده بودن `postex_drop_off` تا شناسهٔ دفتر را می‌گوید؛ مسیر `shipping/collection-type` آزمون API گرفت. فریز روش روی سبد در یادداشت خارج از اسکوپ ماند. Jest فرم `OrderDeliveryStatus` ۴ از ۴، و Jest استرپی مسیر جدید به‌همراه `cartShipping` ۲۰ از ۲۰. مرورگر باز نشد.

## تطبیق فهرست کارها با کد — ۲۳ نوامبر ۲۰۲۶

- دامنه: خواندن کد محلی سه پروژه؛ آزمون تازه‌ای اجرا نشد. شاهدهای قبلی SHIP-FIX-3 (۱۹ نوامبر) و آزمون محدودیت همگام‌سازی شهر سر جایشان‌اند.
- نتیجه: ردیف کدنویسی مبدأ/فرستنده و نمایش برگشتی از فهرست کارهای باز برداشته شد و به پذیرش دستی رفت. ساخت مرسوله از پنل روی سبد است، نه قلم تکی. محدودیت «فرانت هنوز مسیر قدیمی را صدا می‌زند» (۲۰ نوامبر، ساخت مرسوله در سطح سبد) دیگر با کد نمی‌خواند.
- محدودیت: اثرانگشت کلیدها در استقرار واقعی مقایسه نشد و پروفایل نمونه روی ارسال واقعی آزموده نشد. مرورگر برای نمایش برگشتی باز نشد.

## SHIP-FIX-1 — بررسی وضعیت در جزئیات ادمین — ۲۲ نوامبر ۲۰۲۶

- دامنه: فرانت، شاخهٔ `feature/ship-pending-payment`. بای‌مانی فقط سند I11-O. Ledger، کیف پول و متن هشدار لغو عوض نشدند. دکمه از route سروری Next به `POST /api/admin/physical-purchases/{purchaseId}/shipments/refresh` می‌رود. نشان فقط با `carrierPaymentPendingSinceUtc` رسید ادمین در جزئیات ادمین است.
- نتیجه: Jest محلی، ۵ مجموعه و ۴۶ آزمون، ۰ ناموفق. محیط ویندوز. نشان فقط با فیلد پر می‌آید، چهار نتیجهٔ بررسی وضعیت پیام جدا دارند، و route سروری JWT نشست را به مسیر بای‌مانی می‌فرستد.
- محدودیت: دو رفتار زندهٔ پستکس (پرداخت خودکار یا اقدام صریح) و سه نتیجهٔ دیگرِ بررسی وضعیت با کلید واقعی مانده است. مرورگر با نشست ادمین در این اجرا باز نشد.

## SHIP-FIX-4 — S1-06 و S1-02 — ۲۱ نوامبر ۲۰۲۶

- دامنه: سند پستکس و استرپی (`physicalCartService` / `cartShipping` / `physicalCartQuote`). بای‌مانی، فرانت، Ledger و کیف پول دست نخوردند. مهاجرت نیست. پستکس زنده در این اجرا صدا زده نشد؛ شاهد سه‌روشهٔ ۲۱ نوامبر از قبل در بند ۱۰.۱ سند است.
- نتیجه: `npx jest tests/services/physicalCartPrepare.test.js tests/services/physicalCartQuote.test.js tests/api/physicalCartNoOrder.test.js tests/services/cartShipping.test.js --runInBand` در `backend/` — ۲۷ موفق از ۲۷. محیط محلی ویندوز. اثرانگشت برابر مبلغ استعلام را ذخیره می‌کند و تکرارش پستکس را دوباره نمی‌زند؛ اختلاف اثرانگشت بعد از استعلام کوپن را مصرف نمی‌کند و رزرو نمی‌سازد؛ حالت `fixed` بدون تماس با پستکس مبلغ ثابت را ذخیره می‌کند.
- محدودیت: S1-05، S1-01، S1-08 و S3-02 بازند. S1-07 به‌خاطر نبود فیلد هم‌مبنا اجرا نشد.

## ساخت مرسوله در سطح سبد — ۲۰ نوامبر ۲۰۲۶

- دامنه: فقط بای‌مانی. لجر و مبلغ‌ها عوض نشدند. جدول تازه و مهاجرت نیست. مسیر ساخت از خرید تکی به `POST /api/admin/carts/{cartId}/shipments` منتقل شد.
- نتیجه: `CreatePhysicalShipmentTests` + `PostexShipmentCityCodeTests` + `PhysicalDeliverySyncTests` + `RefreshPhysicalShipmentTests` — ۶۶ موفق و ۰ ناموفق. محیط محلی ویندوز، `dotnet test --no-restore`. تجمیع چند قلم، مقیاس تعداد باقی‌مانده، `max(boxTypeId)` و override جعبه، و کلید `BM-{CartId:N}` در آزمون واحد پوشش داده شد.
- محدودیت: Swagger زنده زده نشد. فرانت هنوز مسیر قدیمی را صدا می‌زند (یادداشت TASKS).

## SHIP-FIX-3 — گارد شهر مبدأ و پروفایل نمونه در بای‌مانی — ۱۹ نوامبر ۲۰۲۶

- دامنه: فقط بای‌مانی، شاخهٔ `feature/ship-origin-guards`. لجر، کیف پول و مبلغ‌ها تغییر نکردند. مهاجرت افزودنی `AddCartShippingOriginCityId` بدون backfill و بدون حذف ردیف. پستکس زنده صدا زده نشد.
- نتیجه: `dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj` — ۷۷۸ موفق از ۷۷۸، بدون ردشدن. محیط محلی ویندوز. ناسازگاری `shippingOriginCityId` با `Postex:Origins[].CityId` پیش از فریز `CART_CONFLICT` می‌دهد؛ شهر تهی مسدود نمی‌کند؛ پروفایل نمونه و مبدأ تعریف‌نشده پیش از HTTP رد می‌شوند؛ اثرانگشت SHA-256 فقط در لاگ استارتاپ است.
- محدودیت: ردیف SHIP-FIX-3 باز است. سمت استرپی در شاهد بعدی همین تاریخ بسته شد. S2-08 انجام نشده؛ مهاجرت روی دیتابیس اجرا نشده؛ مرورگر بررسی نشد.

## SHIP-FIX-3 — گارد مبدأ در استرپی — ۱۹ نوامبر ۲۰۲۶

- دامنه: فقط `backend/`، شاخهٔ `feature/ship-origin-guards`. فرانت دست نخورد. پستکس زنده صدا زده نشد.
- نتیجه: `npm test` در `backend/` — Jest ۳۲۸ موفق از ۳۲۸، سپس `node --test` ۹۶ موفق از ۹۶. محیط محلی ویندوز. کالای فیزیکی بدون مبدأ در هر دو حالت `fixed` و `postex` با `CART_CONFLICT` رد می‌شود؛ مبدأ واحد `shippingOriginCityId` می‌دهد؛ چند مبدأ تعارض است؛ ساخت کالا بدون مبدأ به `ramsar` می‌رسد و خالی‌کردن مبدأ در ویرایش رد می‌شود. اثرانگشت SHA-256 پستکس از قبل فقط در لاگ bootstrap بود.
- محدودیت: فرم ادمین استرپی برای رابطهٔ اجباری پیش‌انتخاب بصری ندارد؛ پیش‌فرض هنگام ذخیره اعمال می‌شود. مهاجرت و مرورگر بررسی نشد.
- اصلاح همان روز: diff دست‌نخوردهٔ `{ connect: [], disconnect: [] }` دیگر ویرایش کالا را رد نمی‌کند؛ قطع رابطه بدون جایگزین همچنان رد می‌شود. `npm test`: Jest ۳۳۱ از ۳۳۱، سپس `node --test` ۹۶ از ۹۶.

## SHIP-FIX-2 — وضعیت پستکس، هشدار و مهلت ۳۰ روز — ۱۹ نوامبر ۲۰۲۶

- دامنه: فقط بای‌مانی، روی شاخهٔ `fix/ship-pending-payment-parcel` (شاخهٔ تازه ساخته نشد). فرانت و استرپی تغییر نکردند. مهاجرت افزودنی `AddCarrierAlertAndTrackingUrl` بعد از `AddCarrierParcelState`، بدون backfill.
- نتیجه: `dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj` با `BYEMONEY_CART_PG_TESTS=1` — ۷۶۷ موفق از ۷۶۷. محیط محلی ویندوز. نگاشت ۱۱۳۱۰ به `Returned` و ۱۱۳۱۱ به `InTransit`، هشدار ۱۱۳۰۴/۱۱۳۰۵ بدون تغییر وضعیت، خروج هشدار حل‌نشده و `ClosedWithoutSettlement` از تحویل ۳۰روزه و همگام، و قاعدهٔ تسویه (تأیید خریدار هشدار را نمی‌بندد) در همین مجموعه است. پستکس زنده صدا زده نشد.
- محدودیت: پذیرش Swagger (۴۰۹ برگشتی، `confirmedCount`، نبود شناسهٔ مرسوله در رسید خریدار) و مرورگر فرانت انجام نشد.

## SEP-Q — پاسخ‌های مستند درگاه سپ در I07 — ۱۸ نوامبر ۲۰۲۶

- دامنه: بدون کد؛ استخراج از `مستند فنی نسخه 3.6.pdf` (نگارش ۳.۶)، Postman `OnlinePG.postman_collection 1405.json`، و ضمیمه‌های Government/MultiSettle و بلوپی در `D:\project\مستندات بای مانی\IPG_Doc\`.
- نتیجه: هفت پرسش بخش ۷ بازبینی مرز در [I07 § SEP-Q](../integration/ByeMoney-Strapi-Integration.md#sep-q) ثبت شد؛ بند آستانهٔ مهلت I07 با عمر پیش‌فرض توکن ۲۰ دقیقه و رابطهٔ آن با `noCallbackMinutes` به‌روز شد.
- محدودیت: موارد عملیاتی بدون پوشش در PDF (چند دامنه/IP روی یک ترمینال، زمان فعال‌سازی IP، تطبیق دقیق Redirect با رکورد سپ، API استعلام با ResNum) در جدول I07 به‌صورت «شکاف / هماهنگی با پشتیبانی سپ» مانده‌اند؛ تماس با سپ انجام نشد.

## D19 — «در حال بررسی» و پرداخت مجدد: پذیرش پنج سناریو — ۱۸ نوامبر ۲۰۲۶

- دامنه: بازبینی، پذیرش زنده و یک اصلاح دوخطی پیام؛ بای‌مانی (`master` محلی)، استرپی (`tests/api`، `tests/services`) و فرانت (سبد، نتیجهٔ خرید، `GatewayReviews`، `reviewNotice`). اصلاح سند: D19 §۲ زمان گذشته شد (کلاس حذف شده) و §۶ با متن واقعی پیام و پیش‌فرض ۱۵ دقیقه جایگزین شد.
- نتیجه (محیط محلی ویندوز، PostgreSQL ۱۷): بای‌مانی ۶۶۰ موفق از ۷۳۶ (۷۶ ردشدهٔ پیش‌فرض)؛ با `BYEMONEY_CART_PG_TESTS=1` مجموعهٔ `CartPostgresTests` ۳۹ از ۳۹ موفق — شامل «نتیجهٔ مبهم ← در حال بررسی ← پرداخت مجدد سبد»، «شارژ معلق و پرداخت مجدد با نقل‌قول تازه و حفظ شارژ قبلی»، «تأیید دیرهنگام شارژ قدیمی فقط نور، بدون خرید تکراری» و «بازپرداخت دستی و پرداخت مجدد»؛ `TopUpNoorCreditPostgresTests` ۸ از ۸. استرپی: ۴۳ مجموعه و ۲۹۰ آزمون (`tests/api`، `tests/services`، `tests/config`؛ شامل پروندهٔ `NO_CALLBACK`، آستانه و resolve/reopen). فرانت: ۹ مجموعه و ۱۱۴ آزمون (پیام کاربر، دکمهٔ پرداخت مجدد، دکمه‌های پرونده). کاوش زندهٔ محلی: `GET /api/gateway-reviews/threshold` بدون توکن ۴۰۳.
- محدودیت: اجرای پنج سناریو با مرورگر و درگاه واقعی SEP انجام نشد؛ نتیجهٔ درگاه و اقدام ادمین از همان مسیرهای HTTP واقعی (endpoint یکپارچه‌سازی با کلید سرویس و API ادمین استرپی) ثبت شد.

### اجرای زندهٔ پنج سناریو (همان روز، سه سرویس محلی)

- محیط: بای‌مانی روی `localhost:5000`، استرپی روی `1337`، فرانت روی `3000`. خریدار: کاربر موجود استرپی `342` (‏`t5bp91a46w...`، موجودی صفر)؛ ادمین: کاربر `38` (`m7p3kph...`، دارای `TopUp.Review` در بای‌مانی). توکن‌ها با همان `JWT_SECRET` و issuer/audience استرپی محلی امضا شدند (روش پذیرش قبلی). آستانهٔ استرپی موقتاً ۱ دقیقه شد و پس از آزمون به ۳۰ (مقدار قبلی) برگشت.
- سبد: `05c796d5-...`، دورهٔ `rjmfjwsdlye17u1ojlwrqxxv` به مبلغ ۲۰۱ نور / ۲۰۱۰ ریال.
1. **نتیجهٔ مبهم:** ثبت `Unknown` با کد بانکی `2-` روی `TR-LRMAWMWE` ← شارژ `UnderReview`، بدون رکورد Ledger، کیف پول بدون تغییر، `GET /api/carts/{id}` با `topUpStatus=UnderReview` و `isUnderReview=true`. ✔
2. **پرداخت مجدد همان سبد:** `POST /api/carts/checkout` روی همان شناسهٔ سبد ← `Ready` با `expiresAtUtc` تازه، `topUpRequestId` تهی، `isUnderReview=false`؛ سپس شارژ دوم `TR-S2ZHJJ44` و هر دو شارژ در تاریخچهٔ سبد (وضعیت‌های `UnderReview` / `Pending`). پس از باز شدن پرونده، سرویس پس‌زمینهٔ سبد آن را `Failed` کرد و آماده‌سازی دوباره `Ready` با شناسهٔ رزرو تازه داد — یعنی قفل دائمی نشد. ✔
3. **پروندهٔ «بدون callback»:** تلاش `token_issued` بدون نتیجه (آستانهٔ ۱ دقیقه) ← جاب استرپی پروندهٔ `7a66f9fd-...` با دلیل `NO_CALLBACK` ساخت و تحویل داد (`notification_status=delivered`)؛ بای‌مانی همان شارژ را `UnderReview` با `ReviewCaseId` همان پرونده کرد. ✔
4. **تأیید دیرهنگام شارژ قدیمی:** `Verified` روی `TR-LRMAWMWE` ← `Confirmed`، دو ردیف Ledger با جمع صفر، +۲۰۱ نور به کیف پول، بدون خرید و بدون سبد `Paid`؛ تکرار همان نتیجه ردیف تازه نساخت. ✔
5. **رسیدگی ادمین:** `NO_MATCHING_DEPOSIT` ← `Unresolved`؛ بازگشایی پرونده (revision ۲) ← برگشت به `UnderReview`؛ `MANUAL_REFUND` با مرجع `REF-D19-MANUAL-1` ← `ManuallyRefunded` روی شارژ؛ سپس `Verified` دیرهنگام با ۴۰۹ و کد `REVIEW_MANUAL_REFUND_NOT_SUPPORTED` رد شد. ✔
- اصلاح جانبی (۲ خط + یک رشتهٔ منابع): پیام این رد از متن مخصوص «شارژ تأییدشده» به «پس از ثبت بازپرداخت دستی، نتیجهٔ درگاه پذیرفته نمی‌شود» تغییر کرد تا با عملیات واقعی بخواند. آزمون‌ها: ۲۸ آزمون بازبینی سبز (rebuild با `--artifacts-path` به‌دلیل قفل بودن باینری در حال اجرا) و کل بای‌مانی ۷۳۶ از ۷۳۶ سبز. در یک اجرای قبلی، میزبان آزمون پس از ۷۱۲ آزمون سبز قطع شد؛ در اجرای دوباره کامل و بدون خطا بود.
- نکتهٔ محیطی (نه نقص محصول): تلاش تزریق‌شده با زمان محلی تهران جاب را فعال نکرد، چون استرپی ستون زمان بدون منطقه را با منطقهٔ ماشین تفسیر می‌کند؛ با هم‌ترازکردن زمان، جاب در اولین اجرای دقیقه‌ای پرونده را ساخت.

## خرید یکجای دورهٔ فصلی در کیف پول — ۱۸ نوامبر ۲۰۲۶

- دامنه: بک‌اند استرپی، برنچ `byeMoney`، پس از مرج `origin/master`. قیمت از `chapteredCoursePricing`؛ شناسهٔ فصل از پروفایل، لاگ کیف پول و سفارش درگاه. مبلغ پرداخت قبلی ذخیره یا کسر نمی‌شود. UUID فصل در خرید کیف پول روی کاربر ذخیره می‌شود. محافظ حذف فصل لاگ کیف پول را هم می‌بیند.
- نتیجه: Jest همان چهار فایل ۶۳ موفق؛ `node --test tests/chaptered-course-pricing.test.js` ۷ موفق؛ `node --test tests/order-price-hotfix.test.js tests/pending-checkout.test.js` ۷۵ موفق. محیط محلی ویندوز.
- محدودیت: آزمون PostgreSQL سبد (`BYEMONEY_CART_PG_TESTS`) و مرورگر اجرا نشد. فرانت و بای‌مانی در این نوبت تغییر نکردند.

## SHIP-ADMIN-DISPATCH — دو راه ثبت ارسال — ۱۷ نوامبر ۲۰۲۶

- دامنه: بای‌مانی و پنل ادمین. در «در حال آماده‌سازی» فقط «ارسال با پستکس» (`pick_up` با `ready_to_accept=true`، یا `courier_drop_off`) و «ثبت کد رهگیری دستی» می‌ماند. `Shipped` فقط با کد پستی واقعی. شمارهٔ داخلی یا بارکد ستاره‌ای خرید را `Preparing` نگه می‌دارد. مبلغ ارسال، Ledger و کیف پول عوض نشد.
- اصلاح بازبینی: ثبت کد روی خریدِ دارای شناسهٔ پستکس همان منبع و شناسه را نگه می‌دارد؛ قلمی که در همان نوبت خواندن `Shipped` شده در آن نوبت `Delivered` نمی‌شود؛ اصلاح کدِ ارسال‌شده با کد خالی یا ستاره‌ای رد می‌شود و کد قبلی می‌ماند.
- نتیجه: `ShipTrackingTests`، `PhysicalDeliverySyncTests` و `CreatePhysicalShipmentTests` — ۵۰ موفق. محیط محلی.
- محدودیت: فهرست دفتر پستکس ساخته نشد. پذیرش با مرسولهٔ زنده و مرورگر هنوز در ردیف پذیرش SHIP-TRACKING است.

## SEC-DEV-SECRETS — خروج تنظیمات توسعه از ایندکس گیت — ۱۷ نوامبر ۲۰۲۶

- دامنه: فقط `src/ByeMoney.API/appsettings.Development.json` در مخزن ByeMoney. چرخش کلید را صاحب حساب تأیید کرد. مقدار فایل خوانده یا در سند نوشته نشد.
- نتیجه: `git rm --cached` فایل را از ایندکس برداشت؛ `git ls-files` دیگر آن را نشان نمی‌دهد؛ `.gitignore` خط ۱۲ همان مسیر را می‌پوشاند؛ فایل محلی سر جایش ماند (۱۶۶۴ بایت). `appsettings.json` ردیابی‌شده دست نخورده و `ApiKey` آن خالی است.
- محدودیت: حذف در ایندکس محلی stage شده و هنوز کامیت نشده است. تاریخچهٔ قبلی بازنویسی نشد؛ نسخه‌های قدیمی همان فایل در کامیت‌های گذشته می‌مانند و فقط با چرخش بی‌اعتبارند.

## SHIP-RETURN-RESOLUTION — تعیین‌تکلیف مرسولهٔ برگشتی ادمین — ۱۷ نوامبر ۲۰۲۶

- تغییر ByeMoney: ستون‌های `CarrierReturnResolution`، `CarrierReturnResolutionNote`، `CarrierReturnResolvedAtUtc` و `CarrierReturnResolvedByUserId` با migration افزودنی `AddCarrierReturnResolution` و قید Check؛ متد دامنه‌ای `ResolveCarrierReturn` (برگشتی الزامی، تکرار دقیق idempotence، تکرار متناقض ۴۰۹)؛ حالت `DeliveryConfirmed` کالا را تحویل‌شده و تسویه را باز می‌کند؛ حالت `ClosedWithoutSettlement` کالا را `Shipped` نگه می‌دارد و برای همیشه از تسویه و تحویل خارج می‌کند بدون اثر مالی/Ledger/موجودی؛ قفل تحویل زودهنگام توسط ادمین (۴۰۰) و خریدار (۴۰۹)؛ اندپوینت ادمین `POST /api/admin/physical-purchases/{id}/carrier-return/resolve`؛ نمایش فیلدها در جزئیات سفارش خریدار/ادمین/رسیدها و فیلد تجمیعی `carrierReturnStatus` در فهرست سفارش‌های ادمین.
- آزمون واحد و ادغام: مجموعهٔ جدید `CarrierReturnResolutionTests` (۱۵ موفق)، آزمون‌های تسویه `OrderSettlementTests` (۳۲ موفق) و آزمون‌های HTTP دیتابیسی `PhysicalDeliveryHttpPostgresTests` (۳ موفق).
- باقی: پذیرش از پنل مدیریت با ثبت مرسولهٔ برگشتی واقعی.

## SHIP-RETURN-SETTLEMENT — برگشتی پستکس از تسویه خارج شود — ۱۷ نوامبر ۲۰۲۶

- تغییر ByeMoney: ستون nullable `CarrierReturnedAtUtc` روی `PhysicalProductPurchases` (migration `AddCarrierReturnedAtUtc`)؛ همگام‌سازی پستکس با `Returned` یک‌بار علامت می‌زند بدون تغییر `DeliveryStatus` و بدون Ledger؛ تأیید خودکار ۱۴روزه و قاعدهٔ قابل‌تسویه (D10) سفارش دارای قلم باقی‌ماندهٔ برگشتی را رد می‌کنند. فرانت و استرپی تغییر نکردند.
- آزمون واحد: `PhysicalDeliverySyncTests` و `OrderSettlementTests` (۴۳ موفق، شامل ثبت یک‌بارهٔ برگشتی، رد تأیید خودکار، و حذف از کاندیدای تسویه). rebuild با `--artifacts-path .artifacts/ship-return-test`.
- باقی: پذیرش دستی با مرسولهٔ واقعی برگشتی پستکس و مشاهدهٔ نبودن سفارش در پیش‌نمایش تسویه.

## حدود پذیرش فعلی (هنوز اثبات نشده)

- **JWT واقعی صادرشده از استرپی و محیط مقصد:** تمام شواهد با توکن امضاشدهٔ آزمایشی (HS256 با `Strapi:JwtSecret`) و PostgreSQL محلی‌اند. استرپی staging جدا ندارد.
- **بانک واقعی SEP:** همهٔ پرداخت‌ها با `SEP_MOCK_ENABLED=true` بوده است؛ خرید بانکی بدون مرورگر، قطع سرویس حین پرداخت و پول واقعی آزموده نشده‌اند.
- **پذیرش دستی مرورگر:** فقط برای سبد دوره (۱۸ اکتبر) انجام شده؛ فصل، کالا و ارسال، پنل ادمین و لغو فیزیکی با فرانت و استرپی واقعی انجام نشده‌اند.
- **انتشار:** هیچ شاهدی به معنی ادغام در master یا استقرار نیست. وضعیت شاخه‌ها در DEPLOY-02 است.
- **آزمون‌های مشروط:** آزمون‌های Wallet بدون `BYEMONEY_TEST_CONNECTION_STRING` و آزمون‌های سبد بدون `BYEMONEY_CART_PG_TESTS=1` اجرا نمی‌شوند؛ Skip، قبولی نیست. اجرای یک‌جای همهٔ آزمون‌های دیتابیسی به سقف `max_connections` می‌خورد؛ مجموعه‌ها جدا اجرا شوند.
- **ظرفیت:** فهرست ادمین I11-T بدون فیلتر خریدار برای حجم زیاد اندازه‌گیری نشده است.

## P2-LEGACY-ORDER-CLOSE — بستن ساخت سفارش Legacy استرپی — ۱۰ نوامبر ۲۰۲۶

- تغییر front-end: `/checkout` برای هر سبد به `/cart` می‌رود؛ `ShippingStep`، `executeOnlinePayment` و شاخهٔ legacy سبد حذف شد؛ `POST /api/orders` و `payment/request` با `orderId` استرپی ۴۱۰ می‌دهند؛ `GET /api/orders` و `payment/verify` دست‌نخورده‌اند. Jest کامل ۱۱۱ مجموعه و ۱۱۴۲ آزمون موفق (ESLint اجرا نشد: نبود `eslint.config.*`).
- تغییر backend: `POST /orders/checkout` با کد `ORDER_CHECKOUT_CLOSED` ۴۱۰ می‌دهد و ذخیره نمی‌کند؛ route با احراز api-token ماند؛ `checkout-pricing` ماند (مصرف `discountedPrice` در سبد بای‌مانی). `node --test` (۸۵ از ۸۵) و Jest `tests/api tests/services tests/config` (۳۱ مجموعه، ۲۴۴ آزمون) موفق؛ آزمون‌های `*.pg` اجرا نشدند (نیاز به PostgreSQL آزمایشی). جز route و آزمونش، هیچ فراخوان `orders/checkout` در سه پروژه نیست.
- تصمیم: سفارش‌های pending آنلاین قدیمی حدود روزی یکی‌اند؛ در روز انتشار شمرده و دستی تعیین تکلیف شوند (DEPLOY-05)، بدون سازوکار انقضا.
- باقی: شمارش قبل/بعد رکوردهای orders استرپی در محیط محلی/مقصد با خرید دوره، فصل، کالا و مختلط.

## WALLET-HISTORY — گردش کیف پول کاربر — ۱۰ نوامبر ۲۰۲۶

- تغییر ByeMoney: `GET /api/wallet/transactions` صفحه‌بندی‌شده از Ledger حساب همان کاربر (یک ردیف برای هر تراکنش با سهم هدیه، `orderId` برای خرید سبد و برگشت لغو، `topUpId` برای شارژ) و فیلد `giftBalance` در `GET /api/wallet/balance` (قرارداد I04-W). بدون migration. backend تغییر نکرد.
- تغییر front-end: صفحهٔ `/profile/wallet` (موجودی کل، نور هدیهٔ جدا، گردش با علامت، لینک سفارش و صفحه‌بندی) و پیوند «کیف پول» در منوی پروفایل.
- آزمون ByeMoney با PostgreSQL (`BYEMONEY_TEST_CONNECTION_STRING`): `WalletTransactionsPostgresTests` (۷ آزمون). موارد: برابری جمع گردش با موجودی و جمع سهم هدیه با `giftBalance`، یکی‌شدن ردیف هدیه و عادی خرید (−۵۰ با −۳۰ هدیه)، نبود Ledger کاربر دیگر، صفحهٔ خالی برای کاربر بدون کیف پول، صفحه‌بندی بدون هم‌پوشانی و رد صفحه‌بندی نامعتبر. کل مجموعه ۶۳۹ از ۶۳۹ موفق. چون ByeMoney.API در حال اجرا بود، build با `OutDir=.artifacts/wallet-tx-test` اجرا شد.
- آزمون Jest فرانت: `src/components/profile`، `walletTransactionsApi.test.js`، `useLightStore` و `src/app/cart` (۸ مجموعه، ۵۵ آزمون) موفق. ESLint اجرا نشد چون پروژه پیکربندی `eslint.config.*` ندارد.
- بازطراحی ظاهر (۲۰۲۶-۱۱-۱۰): برچسب `AdminGrant` فقط وقتی کل مبلغ هدیه است «نور هدیه» می‌شود، خطای موجودی پیام دارد و صفحه‌بندی «نمایش بیشتر» است؛ ۸ آزمون Jest کامپوننت پس از آخرین ویرایش ظاهر هم سبز است.
- پذیرش دستی (۲۰۲۶-۱۱-۱۰، گزارش کاربر): انجام و تأیید شد؛ جزئیات سناریو ثبت نشده.
- محدودیت: لینک `orderId` برای خریدهای قدیمی خارج از سبد خالی است.

## D2 — سفارش دستی ادمین روی سبد (دوره و فصل؛ بخش بای‌مانی) — ۸ نوامبر ۲۰۲۶

- تغییر (فقط ByeMoney): `POST /api/admin/carts/orders` (قرارداد I11-AO). همگام‌سازی کاربر به سرویس مشترک `UserProfileSyncService` منتقل شد. migration `AddAdminCartOrderMarkers` (دو ستون nullable) روی دیتابیس توسعه اعمال شد. backend و front-end تغییر نکردند.
- آزمون: `AdminCartOrderTests` (۷ آزمون). موارد: صفر بودن همهٔ سهم‌های سبد رایگان و نمایشی‌ماندن قیمت، پرداخت رایگان بدون Ledger با `IsFree` و علت روی هر قلم، توازن Ledger و کسر برابر total و والد فصل در پرداخت پولی، تفکیک نشانه‌های ادمین، validator (رد کالا، علت فقط در رایگان)، و کسری با 409 `INSUFFICIENT_NOOR_BALANCE` همراه با release رزرو.
- آزمون HTTP با PostgreSQL: `AdminCartOrderHttpPostgresTests` (۵ آزمون، JWT واقعی و RBAC از دیتابیس). موارد: ۴۰۱/۴۰۳ برای بدون توکن، خریدار و ادمین `Purchases.View` بدون ساخت سبد؛ دوره+فصل پولی با تکرار همان `cartId` (یک اثر، کسر ۳۷۰، Ledger خالص صفر)؛ تکرار با حالت دیگر ۴۰۹؛ رایگان (بدون Ledger، علت و ادمین روی سبد، دو فیلد در جزئیات ادمین و نبودشان در `/api/me/orders`)؛ کسری بدون اثر با سبد `Failed` و هم‌گام و release یک‌باره، سپس موفقیت تکرار پس از شارژ؛ رد فصلِ با والد خریده‌شده (۴۰۹) و رد قلم کالا (۴۰۰) بدون اثر.
- کل مجموعه با `BYEMONEY_CART_PG_TESTS=1`: ۶۳۲ از ۶۳۲ موفق. چون ByeMoney.API در Visual Studio باز بود و فایل‌های Debug قفل بودند، build با `--artifacts-path .artifacts/d2` اجرا شد.
- باقی‌مانده: پذیرش با quote واقعی استرپی و JWT واقعی اجرا نشد؛ شکاف کاربر تأییدنشده در quote استرپی (یادداشت خارج از اسکوپ).

### D2 — بخش front-end — ۸ نوامبر ۲۰۲۶

- تغییر (فقط front-end): `ManualOrderForm` فقط دوره و فصل می‌فرستد (فصل با `integrationId` خودش)؛ `POST /api/admin/orders/manual` فقط `POST /api/admin/carts/orders` را با `cartId` کلاینت و JWT نشست سروری صدا می‌زند. ساخت order استرپی، پیش‌چک موجودی، نرخ تبدیل و یادداشت/کوپن حذف شد؛ `purchaseCoursesAsAdminWithByeMoney` حذف شد. پس از ثبت به `/admin/orders?order=cart:<id>` می‌رود و جزئیات، «ثبت دستی توسط» و «علت رایگان» را نشان می‌دهد.
- آزمون Jest: `src/components/admin`، `src/app/api/admin`، `src/lib/admin` و `src/lib` (۶۷ مجموعه، ۶۳۴ آزمون) موفق. قیمت نهایی فرم از `POST /course-carts/admin-preview` استرپی (quote ذینفع) می‌آید و ثبت تا رسیدن آن غیرفعال است؛ قیمت‌ها به نور نمایش داده می‌شوند. موارد تازه: payload دوره/فصل با شناسهٔ فصل، نبود هر درخواست به `/api/orders` استرپی، نگاشت ۴۰۹ کسری، رد ورودی نامعتبر، و ثبات `cartId` در تلاش مجدد پس از کسری (`ManualOrderForm.test.jsx`).
- آزمون دستی ادمین (۲۰۲۶-۱۱-۰۹): ثبت دوره/فصل، رایگان، کسری، تکرار با همان cartId و تخفیف تأیید شد. اصلاح‌های پایانی: متن خطای فروش فصلی با استایل درست (و غیرفعال‌شدن انتخاب کل دورهٔ فصلی) و پیام فارسی مبلغ نامعتبر در مودال شارژ به‌نیابت. باقی‌مانده: حذف endpoint قدیمی در بای‌مانی طبق یادداشت خارج از اسکوپ.

## RBAC-05 — اصلاح اسناد مجوزها — ۱۰ نوامبر ۲۰۲۶

جدول ۱ ماتریس (قالب `PermissionMatrixReflectionTests`) اکنون هر ۸ مجوز را دارد (افزوده‌شدن `Noor.Gift` و `Noor.Withdraw`)، دامنهٔ واقعی `TopUp.Review` و مصرف واقعی `Noor.Inject` (لغو فیزیکی و restock) در آن و در `ByeMoney-Decisions.md` اصلاح شد، و توضیح بالای `adminPermissions.js` هر ۸ مجوز را فهرست می‌کند. شاهد: ۲ آزمون `PermissionMatrixReflectionTests` سبز و ماتریس بازتولید شد؛ ۴ آزمون Jest فرانت سبز. فقط سند و کامنت؛ بدون تغییر منطق. بازبینی مستقل لازم نبود. محدودیت: اجرای کامل آزمون‌ها با فیلتر انجام نشد؛ یک اجرای اول با کرش test host مواجه شد و اجرای دوم با `--no-build` سبز بود.

## RBAC-04 — مجوز `Purchases.View` در فرانت — ۸ نوامبر ۲۰۲۶

- ثابت `PURCHASES_VIEW` و گارد تسویه از قبل بود؛ صفحهٔ `/admin/orders` اکنون فقط بای‌مانی است و پیام `forbidden` را نشان می‌دهد (گارد مسیر لازم نیست). شکاف: تب پیش‌فرض «نیاز به اقدام» `forbidden` را برنمی‌گرداند و هشدار «ارتباط ناقص» می‌آمد؛ در `byeMoneyAdminOrders.js` اصلاح شد. داشبورد با نبود مجوز به آمار استرپی برمی‌گردد (بدون خطا).
- آزمون: `byeMoneyAdminOrders.test.js` (۱۱ آزمون، سبز) شامل سه تب action/all/paid.

## B5-REMOVE-LIGHT — حذف light از فرانت و ادمین — ۸ نوامبر ۲۰۲۶

- تغییر (فقط front-end): فیلد `light` در `adminUsersApi`، `api/admin/users` و GET `admin/orders/manual` با `noorBalance`/`giftBalance` فقط از بای‌مانی و پرچم `balanceUnavailable` جایگزین شد (شکست = `null`، نمایش «نامشخص»). `UsersTable` پس از شارژ ادمین به‌جای جمع محلی از بای‌مانی بازخوانی می‌کند. تأیید فیش سفارش قدیمی دیگر نور را از متن یادداشت یا `totalPrice/1000` حدس نمی‌زند؛ بدون مقدار ساخت‌یافته ۴۰۰ می‌دهد و بای‌مانی صدا زده نمی‌شود. ByeMoney و backend تغییر نکردند.
- آزمون: Jest `src/app/api/admin/orders/[id]/route.test.js` (۳) و `src/lib/admin/__tests__/adminUsersApi.test.js` (۴) تازه، همراه `src/components/admin`: ۱۳ مجموعه، ۹۶ آزمون موفق. کل `src`: ۱۱۳۸ موفق، ۴ شکست در `cart/page` و `checkout/result/page` (مستقل از این تغییر). grep پذیرش `\.light\b|\blight:` در `front-end/src` (غیر آزمون) خالی.
- تست دستی (۸ نوامبر ۲۰۲۶، توسط کاربر): ادمین با `Wallet.View` عدد درست در جدول کاربران و فرم سفارش دستی، ادمین بدون `Wallet.View` «نامشخص»، و بازخوانی موجودی پس از شارژ ادمین، هر سه درست بود. تأیید فیش سفارش قدیمی از `ReceiptModal` دستی آزموده نشد (فقط آزمون خودکار).
- باقی‌مانده: آزمون خودکار رابط کاربری «نامشخص» در جدول/فرم انجام نشده؛ خواندن `AmountNoor` از TopUp به endpoint بای‌مانی نیاز دارد (یادداشت خارج از اسکوپ).

## FE-TESTS-RECHECK — بازبینی شکست آزمون‌های فرانت — ۱۰ نوامبر ۲۰۲۶

- بدون تغییر کد: `npx jest` کامل فرانت: ۱۰۸ مجموعه و ۱۱۶۰ آزمون، همه سبز. `supersededPayment.test.js` (۷ آزمون) و `api/payment` (۴۶ آزمون) سبزند؛ `cart/page` و `checkout/result/page` هم دیگر نمی‌شکنند و `.zwork/` در `testPathIgnorePatterns` هست. شکست‌های قبلی (۲۰۲۶-۱۱-۰۲ تا ۰۸) با تغییرات بعدی (از جمله B5-REMOVE-LIGHT) رفع شده‌اند؛ علت دقیق هر کدام جدا بررسی نشد.

## DEPLOY-06 — آدرس بازگشت درگاه در پروداکشن اجباری — ۸ نوامبر ۲۰۲۶

`getPaymentRedirectUrl` در `front-end/src/app/api/payment/request/route.js` در `NODE_ENV=production` اگر `SEP_REDIRECT_URL` نبود یا `yourdomain.com` داشت خطا می‌دهد؛ بررسی در ابتدای POST و پیش از هر اثر جانبی (ساخت شارژ/تلاش پرداخت) است و پاسخ ۵۰۰ با پیام عمومی می‌دهد. در توسعه fallback به هدر میزبان باقی است. آزمون: Jest `src/app/api/payment` ۵ مجموعه و ۴۶ آزمون موفق (سه حالت: تنظیم‌شده، نبود در پروداکشن، نبود در توسعه). متغیر در چک‌لیست DEPLOY-01 (بند ۷) ثبت شد.

## RBAC-03 — هشدار نبود نقش بای‌مانی در پنل (فرانت‌اند) — ۸ نوامبر ۲۰۲۶

بنر `MissingRoleBanner` در `admin/layout.jsx` وقتی `isLoaded` درست و `permissions` خالی باشد نمایش داده می‌شود (ارجاع به `scripts/assign-admin-role.sql`). بدون تغییر بک‌اند. آزمون خودکار نوشته نشد. تست دستی (ورود با ادمین بدون نقش) توسط کاربر انجام و تأیید شد؛ پذیرش کامل است.

## BND-11b — آدرس تحویل در جزئیات سفارش خریدار (سمت بای‌مانی) — ۸ نوامبر ۲۰۲۶

- تغییر: فیلد افزودنی nullable `shippingAddress` (بدون `version`) در `GET /api/me/orders/{orderId}` فقط برای سبد `Paid` دارای کالا؛ بدون مهاجرت و بدون تغییر منطق مالی؛ جزئیات ادمین، رسید و فهرست‌ها دست نخوردند.
- آزمون: `dotnet test` با `BYEMONEY_CART_PG_TESTS=1` و PostgreSQL محلی ۶۲۰ موفق، ۰ شکست. HTTP واقعی با JWT: وجود آدرس (۶ فیلد، بدون version) در سفارش پرداخت‌شدهٔ خریدار، نبودن در سبد پرداخت‌نشدهٔ دارای کالا و آدرس، `404` برای کاربر دیگر. سفارش بدون کالا روی HTTP جدا آزمون نشد (دامنه برای سبد بدون کالا آدرس نگه نمی‌دارد).
- فرانت (۸ نوامبر ۲۰۲۶): `route.js` فقط شش فیلد نمایشی `shippingAddress` را از پاسخ بای‌مانی عبور می‌دهد و `ByeMoneyOrderView` آن را در کارت «ارسال کالا» نشان می‌دهد؛ بدون اتصال به استرپی. Jest: `src/app/api/orders`، `src/app/profile/orders`، `src/components/profile` موفق؛ `src/lib/__tests__/supersededPayment.test.js` (callback پرداخت SEP) بررسی شد: ۲ شکست نامرتبط با BND-11b بود. (الف) جست‌وجوی `findAttempt` (تلاش شارژ درگاه) اولین `fetch` را می‌گیرد و mock آزمون عقب مانده بود؛ در آزمون اصلاح شد و ۶ از ۷ موفق است. (ب) حالت `OK` با مبلغ مغایر: رفتار کد درست تأیید شد (برگشت وجه از طریق `ReverseTransaction` سپ به کارت/حساب بانکی پرداخت‌کننده، نه کیف پول؛ و سفارش `canceled` + `failed`). آزمون اصلاح شد: این حالت از جدول «سفارش باز می‌ماند» جدا و با آزمون مستقل نوشته شد. نتیجه: `supersededPayment.test.js` ۷ از ۷؛ Jest کامل `src/app/api`، `src/app/profile`، `src/components/profile`، `src/lib` ۶۷ مجموعه و ۸۵۵ آزمون موفق.

## BND-11 (سمت بای‌مانی) — نسخهٔ ثابت آدرس تحویل روی سبد — ۸ نوامبر ۲۰۲۶

- تغییر: ستون‌های nullable `ShippingAddress_*` روی `Carts` (migration افزودنی `AddCartShippingAddress`)؛ آداپتور استرپی برای سبد دارای کالا بدون آدرس کامل یا `addressVersion` معتبر پاسخ را نامعتبر (`CART_INVALID`، سبد `Failed`) می‌کند؛ بازگشایی سبد آدرس را پاک و prepare تازه جایگزین می‌کند؛ سبد `Paid` تغییر نمی‌کند؛ `shippingAddress` فقط در `GET /api/admin/orders/{orderId}`.
- آزمون: `dotnet test ByeMoney.UnitTests` با `BYEMONEY_CART_PG_TESTS=1` و PostgreSQL محلی ۶۲۰ موفق، ۰ شکست (خروجی جدا چون API در حال اجرا فایل‌ها را قفل کرده بود). پوشش جدید: ذخیره و جایگزینی در بازگشایی (PostgreSQL)، رد پاسخ ناقص (۵ حالت)، سبد دوره بدون آدرس، سبد `Paid` ثابت، و نبود آدرس در جزئیات/رسید/فهرست خریدار و فهرست/رسید ادمین روی HTTP واقعی با JWT.
- فرانت و استرپی (بازبینی ۲۰۲۶-۱۱-۰۸): پنل ارسال ادمین آدرس را از `shippingAddress` جزئیات ادمین بای‌مانی می‌خواند و `fetchCartAddress`، هشدارهای استرپی و نمایش آدرس در سفارش خریدار حذف شد؛ `GET /api/byemoney-carts/:cartId/address` و مجوزش از استرپی حذف شد. آزمون: backend `npx jest --runInBand tests/api tests/services tests/config` ۲۱۸ موفق؛ فرانت (`src/lib/admin`، `src/app/api/orders`، `src/app/profile/orders`) ۱۵۹ موفق. آدرس سفارش جدید بدون وابستگی به استرپی خوانده می‌شود (پوشش آزمون خودکار؛ پذیرش دستی با JWT واقعی انجام نشده).

## BND-12 — کلیدهای سرویس متفاوت در تنظیمات توسعه — ۸ نوامبر ۲۰۲۶

- تغییر: در `appsettings.Development.json` مقدار `STRAPI_TO_BYEMONEY_SERVICE_KEY` با مقدار `backend/.env` و `front-end/.env.local` یکی شد و `FRONTEND_TO_BYEMONEY_SERVICE_KEY` (هم‌مقدار `front-end/.env.local`) افزوده شد. `ServiceKey` و `BYEMONEY_SERVICE_KEY` یک جهت‌اند (بای‌مانی به استرپی؛ متغیر محیطی روی `ServiceKey` غلبه دارد) و مقدار مشترک ماندند، با مقدار `BYEMONEY_SERVICE_KEY` استرپی و فرانت هم‌خوان.
- نتیجه: سه جهت (بای‌مانی←استرپی، استرپی←بای‌مانی، فرانت←بای‌مانی) سه مقدار متفاوت ۳۲+ بایتی دارند؛ JSON معتبر است.
- آزمون: `dotnet test` اجرا نشد چون API در حال اجرا فایل‌ها را قفل کرده است؛ آزمون‌های واحد فقط رشتهٔ اتصال را از این فایل می‌خوانند. اجرای محلی کامل (استرپی/فرانت با API ری‌استارت‌شده) دستی بماند.

## BND-10 — حذف دو endpoint بی‌استفادهٔ نتیجهٔ درگاه — ۸ نوامبر ۲۰۲۶

- بررسی: فرانت و استرپی هیچ فراخواننده‌ای برای `gateway-confirmations` و `gateway-cancellations` نداشتند (فرانت از `gateway-results` و استرپی با `GatewayResultKeyFilter` کار می‌کنند).
- تغییر: دو مسیر، `ServiceKeyFilter`، چهار DTO مربوط و آزمون کنترلر حذف شد؛ مسیرهای `GatewayResultKeyFilter` دست نخورد.
- پاک‌سازی تکمیلی: handler و validator فرمان Confirm و کل فرمان Cancel (handler، validator و `CancelAsync` سرویس) با آزمون‌هایشان حذف شد؛ `ConfirmAsync` برای ثبت نتیجهٔ درگاه ماند. کلید resx `TopUpRequest_GatewayCancelled` و متد دامنه `CancelGateway` (در آزمون‌ها مصرف می‌شود) ماندند.
- آزمون: `dotnet test ByeMoney.UnitTests` (خروجی جدا چون API در حال اجرا فایل‌ها را قفل کرده بود) ۶۰۸ موفق، ۰ شکست (۱۶ آزمون کد حذف‌شده برداشته شد). آزمون HTTP برای ۴۰۴ مسیرهای حذف‌شده نوشته نشد؛ حذف اکشن‌ها کافی است.

## کالا در مسیر قدیمی TopUpCheckout — ۸ نوامبر ۲۰۲۶

- نتیجه: مشکل در جریان واقعی رخ نمی‌دهد؛ `PendingItemType` فقط `Course` دارد، validator نوع نامعتبر را رد می‌کند (آزمون موجود در `CreateTopUpRequestCommandTests`) و شارژ سبد `PendingItems` ندارد. فصل هم رد می‌شود چون endpoint کاتالوگ استرپی فقط با `documentId` دوره پاسخ می‌دهد. فقط سند اصلاح شد؛ آزمونی اجرا یا اضافه نشد.

## D4 — خروج نور توسط ادمین — ۸ نوامبر ۲۰۲۶

| موضوع | نتیجه و محدودیت |
|---|---|
| build و migration | `dotnet build ByeMoney.sln --artifacts-path .artifacts` موفق بود. مسیر خروجی جدا لازم شد، چون API در Visual Studio اجرا بود و bin قفل بود. migration `AddNoorWithdrawals` روی `ByeMoney_Dev` اعمال شد |
| آزمون‌ها | `dotnet test` کامل: ۶۲۱ موفق، ۰ ناموفق، ۰ ردشده. شامل `NoorWithdrawalTests` (اعتبارسنجی و انتقال وضعیت) و ۹ آزمون `NoorWithdrawalPostgresTests` روی PostgreSQL 17 محلی |
| پوشش PostgreSQL | کسر فقط از نور عادی و Ledger متوازن. رد خروج با نور هدیه. ثبت یک‌جای Paid. شش تکرار هم‌زمان یک `operationId` فقط یک کسر ساخت و محتوای متفاوت `409` گرفت. خروج و خرید هم‌زمان روی آخرین نور: فقط یکی موفق شد. ثبت واریز و «پرداخت نشد» هم‌زمان: یک نتیجه. سه «پرداخت نشد» تکراری فقط یک جبران ساخت (T25). استرداد پرداخت تکراری: سقف مبلغ شارژ، یک استرداد فعال برای هر شارژ، امکان استرداد دوباره پس از جبران، و رد شارژ کاربر دیگر. Down و Up migration با داده: Wallet با Ledger تطبیق ماند |
| بازبینی مستقل مالی | مشکل جدی نیافت. فاصلهٔ سند D09 («حساب مبدأ» و Audit Log) و شکل خطای ۴۰۰ در I11-W اصلاح شد. موارد نظری در یادداشت‌های خارج از اسکوپ ثبت شد |
| فرانت (پنل ادمین) | صفحهٔ «خروج نور»، فرم خروج از ردیف کاربر، «پرداخت شد/نشد» و نمایش فیش، همه از route سروری با گارد `Noor.Withdraw`. قرارداد با I11-W تطبیق داده شد. در بازبینی، استایل اصلاح شد: دکمه، select و input فایل با فونت سیستم دیده می‌شدند و چند متغیر CSS تعریف نشده بود. Jest محلی: ۸ مجموعه، ۶۵ موفق (D4، کاربران، اجزای مشترک ادمین). کامپایل SCSS بررسی شد. نمایش در مرورگر دیده نشد. backend (استرپی) درگیر نیست |
| اصلاح تک‌مبلغی (۸ نوامبر ۲۰۲۶) | ادمین فقط ریال می‌دهد و نور را سرور حساب می‌کند (نرخ فعلی یا نرخ ثبت‌شدهٔ شارژ در استرداد). مسیر تازهٔ `refundable-topups` اضافه شد. بدون migration. `dotnet test` کامل: ۶۲۵ موفق، ۰ ناموفق (آزمون‌های PostgreSQL فعال بودند). آزمون‌های تازه: تبدیل با نرخ فعلی، استرداد با نرخ شارژ (مبلغ جزئی)، تکرار پس از تغییر نرخ بدون کسر دوم، ریال با نور صفر، فهرست شارژها و پرچم استرداد فعال. بازبینی مستقل مالی برای این اصلاح انجام نشده است. فرانت هنوز با قرارداد قدیمی است و تا اجرای پرامپت فرانت `amountNoor` می‌فرستد (نادیده گرفته می‌شود) |
| پذیرش دستی (۸ نوامبر ۲۰۲۶) | مالک خروج نور را از پنل ادمین در مرورگر آزمود و تأیید کرد (T24). محدودیت: جزئیات سناریوها ثبت نشده؛ پذیرش با JWT استرپی واقعی در محیط مقصد جزو DEPLOY است |

## آخرین شواهد — ۳ تا ۵ نوامبر ۲۰۲۶

| موضوع | نتیجه و محدودیت |
|---|---|
| R08 — پیام کمبود موجودی (۷ نوامبر) | بای‌مانی: ۵۹۹ آزمون واحد و ۵۲ آزمون سبد/کلاینت روی PostgreSQL سبز (نگاشت `lineError` به `CART_STOCK_UNAVAILABLE`، شکست سبد و release یک‌باره بدون کسر نور). فرانت: آزمون‌های Jest پیام و صفحهٔ سبد سبز؛ دو شکست قدیمی `supersededPayment.test.js` پیش از تغییر هم وجود داشت. بک: `npm run test:pg` با `PHYSICAL_STOCK_PG_TESTS=1` (۱۸ قبول، ۲ ردشده مربوط به متغیر کوپن). پذیرش دستی با استرپی و فرانت واقعی (۸ نوامبر، توسط کاربر): دو خریدار روی آخرین واحد، برگشت موجودی در شکست/انقضا، لغو پرداخت‌شده با تراز صفر Ledger و پیام تعداد مجاز هر چهار درست بود. |
| **BND-09 — نام عام قلم، مسیرهای `carts`/`orders`، قاعدهٔ فصل در آداپتور (۷ نوامبر)** | بای‌مانی: `ExternalCourseId` قلم سبد به `ExternalItemId` (migration `RenameCartItemExternalItemId` فقط `RenameColumn`؛ Up/Down/Up روی `ByeMoney_Dev` و آزمون PostgreSQL `ExternalItemIdRenameMigrationKeepsCartRowsAcrossDownAndUp` با ردیف سبد و پرداخت پس از بازگشت)؛ مسیرها `api/carts`، `api/me|admin/orders`، `api/admin/order-settlements`، فقط `api/purchases` و `api/admin/purchases`؛ `CourseOrder*` به `Order*`؛ ورودی checkout فقط `items`؛ پاسخ‌ها `items[]`، `externalItemId`، `itemTitle`. قاعدهٔ فصل/دوره از Domain به `CartPricingClient` رفت (قرارداد prepare استرپی بدون تغییر). `dotnet build` سبز؛ `dotnet test` با `BYEMONEY_CART_PG_TESTS=1`: ۵۸۹ موفق، ۰ شکست، ۰ Skip (۹ آزمون تازه: نگاشت آداپتور، شکل درخواست سیمی، قاعدهٔ عام والد، migration). `Permissions-Matrix.md` بازتولید شد. فرانت: jest کامل ۱۰۷۵ موفق، ۲ شکست قدیمی `supersededPayment` (ردیف P2 جدا)؛ grep مسیرهای `course-carts/checkout`، `course-orders`، `course-purchases`، `course-order-settlements` بای‌مانی صفر. استرپی بدون تغییر کد: `chapterCart`، `cartPreview`، `physicalCartNoOrder` ۴۲ موفق. پذیرش دستی: مالک سبد، خرید و دوره را در مرورگر آزمود (۸ نوامبر). محدودیت: فصل، سبد مختلط با کالا، جزئیات/رسید ادمین و تسویه در آزمون نهایی مالک؛ بای‌مانی و فرانت باید هم‌زمان منتشر شوند (alias قدیمی نیست). |
| **BND-08 — عمر کوتاه توکن و CSP (۷ نوامبر)** | تصمیم مالک: عمر توکن ۱ ساعت، نشست next-auth ۳۰ روز لغزان. استرپی: `JWT_EXPIRES_IN` (پیش‌فرض `1h`) و `POST /api/auth/token/refresh` فقط با استراتژی api-token. توکن منقضی تا ۳۰ روز پذیرفته می‌شود؛ کاربر مسدود، ناموجود یا با documentId ناهمخوان ۴۰۱ می‌گیرد. `strapi routes:list` مسیر را نشان داد. Jest (`tests/api tests/services tests/config`): ۳۰ مجموعه، ۲۲۲ آزمون سبز (۱۱ آزمون تازهٔ تمدید و پیکربندی). backend توکن کاربر را ذخیره نمی‌کند (سرویس همگام‌سازی هر بار توکن تازه می‌سازد)، پس عمر کوتاه آن را نمی‌شکند. فرانت: تمدید در callback `jwt` پس از نیمهٔ عمر. قطعی موقت با توکن هنوز معتبر نشست را نگه می‌دارد؛ رد ۴۰۱ نشست را `SessionExpired` می‌کند و مرورگر خارج می‌شود. `refetchInterval` ۱۰ دقیقه و تازه‌سازی هنگام بازگشت به تب. همگام‌سازی بای‌مانی به کاربر وابسته شد، نه توکن. CSP ایستا در `headers()`، با `CSP_REPORT_ONLY=true` برای staging. انحراف از پلن: `img-src`/`media-src` هر https را مجاز می‌کنند، چون محتوای ادمین نشانی آزاد دارد؛ HLS درس از دامنه‌های رسانه در `connect-src` می‌آید. jest: ۱۰ آزمون تازه سبز؛ کل مجموعه ۱۰۷۵ موفق و ۲ شکست قبلی (`supersededPayment`، ثبت در P2) به‌علاوهٔ کپی‌های `.zwork`. `next build` موفق. اجرانشده: پذیرش staging (ردیف P3). |
| **BND-07 — مسیرهای ادمین بای‌مانی فقط از route سروری Next (۷ نوامبر)** | فرانت: چهار route نازک تازه (`/api/admin/noor-gifts`، `/api/admin/topups/assisted` با multipart فیش، `/api/admin/wallets/batch-balances`، `/api/admin/course-orders/[orderId]/receipt`) که JWT را فقط از نشست سروری می‌خوانند (بدون نشست ۴۰۱، غیرادمین ۴۰۳) و پاسخ و کد خطای بای‌مانی را بی‌تغییر برمی‌گردانند؛ هدیهٔ نور، شارژ کمکی، موجودی فرم سفارش دستی و فهرست کاربران و رسید کنترل ارسال به آن‌ها منتقل شدند و نرخ تبدیل (عمومی) بی‌توکن خوانده می‌شود. آزمون نگهبان: هیچ فایل `'use client'` توابع ادمین `byeMoneyApi` را import نمی‌کند. ۶ شکست قدیمی `AdminNoorGiftModal` رفع شد (آزمون با مودال تک‌مرحله‌ای ناهماهنگ بود). Jest مسیرهای ادمین، کامپوننت‌های ادمین و سفارش: ۲۴ مجموعه، ۲۳۱ آزمون سبز؛ کل مجموعه فقط شکست‌های شناخته‌شدهٔ `supersededPayment` و کپی‌های `.zwork`. سند D20 بند ۵ و I02 اصلاح شد (jwt ادمین در نشست مرورگر می‌ماند؛ ریسک باقی‌مانده با BND-08). پذیرش دستی DevTools در مرورگر باقی است. |
| **BND-06 — شناسهٔ رزرو commit‌شده برای خواندن آدرس (۷ نوامبر)** | بای‌مانی: فیلد افزودنی `reservationId` در DTO جزئیات سفارش (`/api/me` و `/api/admin`)، فقط برای سبد `Paid` و برابر رزرو جاری؛ بدون migration. آزمون واحد: سبد بازگشایی‌شده و سپس پرداخت‌شده رزرو تازه و متفاوت با `cartId` می‌گیرد؛ سبد پرداخت‌نشده، `TopUpCheckout` و `Purchase` مقدار `null`. کل مجموعه با `BYEMONEY_CART_PG_TESTS=1`: ۵۸۰ آزمون سبز. فرانت: پنل ادمین و جزئیات سفارش کاربر آدرس را با `reservationId` می‌خوانند، بدون آن آدرس خوانده نمی‌شود و شناسه به مرورگر نمی‌رسد؛ ۴۵ آزمون Jest در دو مجموعه سبز. پذیرش با سبد واقعی بازگشایی‌شده در مرورگر باقی است. |
| **ADMIN-ORDERS-REDESIGN (۷ نوامبر)** | `/admin/orders` فقط سفارش‌های بای‌مانی: تب‌های «نیاز به اقدام / همه / پرداخت‌شده / در انتظار / ناموفق» با شمارنده، تب و جستجو و فیلتر «موجودی برنگشته» و سفارش بازشده در query string، جدول شش‌ستونه، خط هشدار (قطعی بای‌مانی و موجودی برنگشته)، toast، و کشوی جزئیات سه‌تبی (`OrderDrawer`) که در `/admin/orders/settlements` هم جایگزین ردیف بازشونده شد. «نیاز به اقدام» = سفارش Paid با کالای ارسال‌نشده (جزئیات هر کاندید خوانده می‌شود، سقف ۱۰۰ سفارش اخیر؛ ارسال‌شده‌ها حذف) + سفارش‌های با موجودی برنگشته؛ داده از route سروری `/api/admin/course-orders` و `byeMoneyAdminOrders.js` بدون تغییر API بای‌مانی/استرپی. سفارش‌های استرپی در `/admin/orders/legacy` (بدون منطق بای‌مانی، فیلتر وضعیت و دکمهٔ تغییر وضعیت؛ جزئیات سه‌ستونه) و در زیرمنو؛ «ثبت دستی» فقط دکمهٔ سرصفحه. اعلان «موجودی برنگشته» و پیام تکراری «تحویل کالا نهایی شده» بازطراحی/حذف شد؛ باگ `isPhysicalFullyCancelled` در پنل (خواندن از `data.items` به‌جای `data.order.items`) اصلاح شد. آزمون: `src/components/admin/Orders` و `src/lib/admin` سبز (۳۴ و ۸۳ آزمون، شامل آزمون جدید `byeMoneyAdminOrders.test.js`)؛ کل مجموعهٔ مرتبط جز دو مجموعهٔ از پیش شکستهٔ نامرتبط (`AdminNoorGiftModal`، `supersededPayment`) سبز. بررسی مرورگر با داده و JWT واقعی را کاربر انجام داد؛ `next build` اجرا نشد. **محدودیت:** تب «نیاز به اقدام» به SHIP-TRACKING وابسته است. |
| **ADMIN-SETTLEMENT-BYEMONEY — بخش front-end (۷ نوامبر)** | صفحهٔ `/admin/orders/settlements` (گارد ورود `Purchases.View`) با چهار route سروری Next زیر `/api/admin/course-order-settlements` که JWT را فقط از نشست ادمین می‌خوانند (BND-07): دورهٔ جاری با تعداد، سه عدد نور عادی/هدیه/جمع و نکتهٔ نبود پشتوانهٔ ریالی نور هدیه؛ دکمهٔ بستن فقط با `Purchases.Settle` و غیرفعال در تعداد صفر؛ پنجرهٔ تأیید با همان اعداد پیش‌نمایش و ارسال عین `previewToken`؛ پس از موفقیت و هر `409` پیام نمایش و پیش‌نمایش و آرشیو تازه می‌شوند؛ ۴۰۳ پیام می‌دهد و صفحه نمی‌شکند؛ آرشیو بدون ویرایش/حذف با سفارش‌های هر دوره. ردیف‌ها نام و موبایل خریدار را (route سروری با یک پرس‌وجوی `documentId` از استرپی؛ شکست آن فقط نام را خالی می‌گذارد)، شمارهٔ کوتاه سفارش همانند صفحهٔ سفارش‌ها، تاریخ سپس ساعت، و با کلیک پنل جزئیات موجود سفارش را نشان می‌دهند؛ نام ادمین بستن‌کننده هم به همین روش. زیرمنوی «سفارش‌ها» در منوی کناری با فرزند «تسویه و آرشیو» (فیلتر با `Purchases.View`) ساخته و آیتم «ثبت دستی» طبق پلن تسک ۱ از منو حذف شد. تسویهٔ استرپی دست نخورد. ۲۱ آزمون Jest جدید (کلاینت API، کمک‌کار route و افزودن نام، کامپوننت، زیرمنو) سبز؛ کل مجموعهٔ `src` سبز جز دو مجموعهٔ از پیش شکسته و نامرتبط (`supersededPayment`، `AdminNoorGiftModal`؛ بدون این تغییر هم شکست می‌خورند). **محدودیت:** اجرا در مرورگر با بای‌مانی و JWT واقعی و `next build` انجام نشد؛ قرارگرفتن در منو و کشوی جزئیات در ADMIN-ORDERS-REDESIGN است. |
| **ADMIN-SETTLEMENT-BYEMONEY — بستن دورهٔ تسویهٔ سفارش‌های بای‌مانی، بخش ByeMoney (۷ نوامبر)** | migration `AddOrderSettlementPeriods` روی دیتابیس توسعه اعمال شد؛ `Down` و سپس `Up` روی همان دیتابیس هم موفق بودند. این migration تبدیل داده ندارد و جدول‌های جدید هنگام `Down` خالی بودند. جدول‌های `OrderSettlementPeriods` و `OrderSettlementItems` (کلید اصلی `OrderId`) و seed مجوز `Purchases.Settle` برای Admin ساخته شدند. ۲۴ آزمون واحد: قاعدهٔ قابل تسویه (فقط‌دوره، پرداخت‌نشده، ارسال‌نشده، ارسال ناقص، لغو پیش از ارسال، لغو جزئی تعداد، لغو کامل)؛ مبلغ خالص با لغو واقعی و برگشت سهم عادی/هدیه؛ تغییر پیش‌نمایش با `previewToken`، شامل جایگزینی سفارش تسویه‌شده با سفارش هم‌مبلغ (یافتهٔ بازبینی مستقل)؛ دورهٔ خالی؛ حذف سفارش تسویه‌شده؛ validator. ۳ آزمون PostgreSQL: دو دورهٔ پیاپی بدون تکرار سفارش؛ سفارش ارسال‌نشده پس از ارسال در دورهٔ بعد آمد؛ دو بستن هم‌زمان (هر دو پیش از درج هم‌گام شدند) فقط یک موفقیت و یک `409 SETTLEMENT_ALREADY_SETTLED` دادند؛ قید کلید، سفارش تکراری در دورهٔ دوم را بدون ثبت هیچ ردیفی رد کرد؛ تعداد ردیف‌های Ledger ثابت ماند. تفکیک نور عادی/هدیه/جمع (تصمیم کاربر همان روز): migration بازتولید و `Down`/`Up` دوباره موفق؛ آزمون واحد تفکیک پس از لغو و حذف سفارش بدون سهم ثبت‌شده؛ در PostgreSQL سبد با پرداخت واقعی ثبت شد و سهم ارسال از Ledger خوانده شد (۱۱۰ عادی، ۰ هدیه) و سفارش فقط‌دوره ۳۰/۱۰ بود. کل مجموعه با `BYEMONEY_CART_PG_TESTS=1`: ۵۷۸ قبول و ۰ Skip. بازبینی مستقل مالی: بدون ایراد مسدودکننده؛ دو یافته اصلاح شد و خواندن کامل داده مؤجل شد. **محدودیت:** HTTP با JWT واقعی آزموده نشده است (بخش front-end در ردیف بالا). |
| **BND-05 — نمایش شکست دائمی برگشت موجودی در `/admin/orders` (۷ نوامبر)** | فرانت: `getPendingPhysicalRestocks` (سروری، با JWT نشست) و هشدار `AbandonedRestocksAlert` فقط برای ردیف‌های `abandoned` با سبد، تلاش‌ها، کد خطا و آخرین تلاش؛ شکست دریافت (مثلاً ۴۰۳ بدون `Noor.Inject`) «نامشخص» نشان می‌دهد، نه سکوت. ۳۸ آزمون Jest در دو مجموعهٔ مرتبط سبز. بای‌مانی تغییر نکرد. محدودیت: نمایش در مرورگر با ردیف رهاشدهٔ واقعی آزموده نشده؛ بازبینی مستقل انجام نشد (تغییر کوچک فرانت‌محور و بدون منطق مالی). |
| **BND-05b — بازطراحی هشدار برگشت موجودی (۷ نوامبر)** | هشدار بالای فهرست به یک خط (تعداد و شمارهٔ سفارش‌ها) کاهش یافت؛ ردیف سفارش برچسب «موجودی برنگشته» گرفت و توضیح با نام کالا، علت به زبان ساده و دو دکمهٔ «تلاش دوباره» (یک تلاش دستی بدون تغییر شمارنده؛ در شکست، عملیات رهاشده می‌ماند و به صف خودکار برنمی‌گردد و علت تازه نمایش داده می‌شود) و «موجودی را دستی اصلاح کردم» (بستن با نشان `RESTOCK_RESOLVED_MANUALLY`، ثبت ادمین در لاگ، بدون migration) داخل جزئیات همان سفارش آمد. بای‌مانی: ۲ آزمون جدید تلاش دوباره و ۱ آزمون بستن دستی، مجموعهٔ لغو فیزیکی ۲۵ آزمون سبز. فرانت: ۳۸ آزمون Jest سبز. محدودیت: در مرورگر با ردیف رهاشدهٔ واقعی آزموده نشد. |
| **BND-04 — حذف `GatewayTopUpExpiryPolicy` و آستانهٔ پیام کاربر (۷ نوامبر)** | کلاس بلااستفادهٔ بای‌مانی حذف شد (بدون ارجاع دیگر؛ migration لازم نیست). استرپی: `GET /api/gateway-reviews/threshold` (I07 بند ۷)، ۲۹ مجموعه و ۲۱۰ آزمون Jest سبز (`npx jest --runInBand tests/api tests/services tests/config`). فرانت: route سروری و هوک، ۴ مجموعه و ۶۵ آزمون سبز؛ پیام سبد و نتیجهٔ پرداخت عدد تنظیم را نشان می‌دهد. بای‌مانی: بیلد بدون خطا و ۵۴۷ از ۵۴۷ آزمون با PostgreSQL محلی سبز (۲۰۲۶-۱۱-۰۷). آزمون دستی محلی: با JWT محلی، تغییر آستانه به ۴۵ توسط ادمین در استرپی، خواندن آن توسط کاربر عادی، رد مقدار ۰ (۴۰۰) و نمایش پیام در مرورگر (نتیجهٔ مرورگر طبق گزارش کاربر). endpoint کاربر بدون توکن ۴۰۳ می‌دهد (policy `is-authenticated-user`؛ پیش‌تر عمومی بود و اصلاح شد؛ ۲۱۱ آزمون Jest استرپی سبز). محدودیت: شاهد محلی با درگاه ماک است؛ بانک واقعی و محیط مقصد آزموده نشده. |
| **BND-01 — رقابت لغو و ارسال (۵ نوامبر)** | تغییر وضعیت ارسال زیر قفل خریدار و سبد همان لغو اجرا می‌شود (`PhysicalDeliveryStore.ExecuteLockedAsync`). آزمون‌های PostgreSQL (هر کدام ۱۲ تکرار): لغو و ارسال هم‌زمان هرگز هر دو موفق نمی‌شوند؛ دو ادمین هم‌زمان (`Delivered` و `Shipped`) همیشه `Delivered` نهایی می‌دهند. انتقال نامعتبر اکنون `ConflictDomainException` و HTTP ۴۰۹ می‌دهد. کل ByeMoney با PostgreSQL: ۵۳۸ موفق، بدون شکست. شاهد محلی است. |
| **لغو فیزیکی و برگشت نور (C8، I09-PC) — ۳ نوامبر** | ۱۰ آزمون PostgreSQL و ۵ آزمون واحد: تقسیم ۳۳/۳۳/۳۴ با جمع دقیق سهم عادی/هدیه، برگشت ارسال فقط در لغو کامل مرسوله، Ledger معکوس متوازن، سقف برگشت در Application و قید دیتابیس، تکرار و هم‌زمانی بدون برگشت دوم، عدم release کوپن سبد پرداخت‌شده، ۴۰۹ پس از ارسال، عدم بازمحاسبهٔ قیمت پس از لغو جزئی. محدودیت: آزمون دستی سرتاسری با استرپی و فرانت واقعی و بازبینی مستقل مالی انجام نشده است. |
| **I11-O: رسید ادمین و تحویل با JWT واقعی در میزبان کامل ASP.NET — ۳ نوامبر** | `PhysicalDeliveryHttpPostgresTests`، RBAC واقعی از دیتابیس. بدون توکن ۴۰۱، بدون مجوز ۴۰۳، رسید ادمین با `Purchases.View` برابر I11-O، رسید `/me` کاربر دیگر ۴۰۴، انتقال‌های مجاز و نامجاز تحویل (۲۰۰ و ۴۰۰)، بدون اثر روی Wallet و Ledger. محدودیت: توکن را استرپی صادر نکرده است. |
| **آدرس تحویل ادمین و فهرست تلاش‌های درگاه — ۳ نوامبر** | درخواست HTTP واقعی به استرپی محلی: `GET /api/byemoney-carts/:cartId/address` با full-access ۲۰۰، با read-only ۴۰۳، سبد ناموجود ۴۰۴؛ `GET /api/gateway-payment-attempts` با `filters[topUpRequestId][$in]` ۲۰۰ (برای read-only هم). ایندکس و migration `2026.11.01T12.00.00` بررسی شد. محدودیت: سبد آزمون دارای کالای فیزیکی تأییدشده نبود؛ رفتار پنل در مرورگر اجرا نشد. |
| **تخصیص پرداخت واقعی هر قلم و DB-04 (فاز ۱) — ۳ نوامبر** | migration `AddPaymentSharesAndTopUpCartLink` (`Up` از دیتابیس خالی؛ `Down` سپس `Up` روی توسعه). سهم عادی/هدیه روی `CoursePurchase`، تفکیک Ledger هر قلم و ارسال، خواندن سابقهٔ شارژ از `TopUpRequests.CartId`. کل مجموعه ۵۰۳ از ۵۰۳. محدودیت: خریدهای قدیمی سهم null دارند (طبق تصمیم پروژه backfill نیست). |
| **DB-08 — ایندکس یکتای خرید دوره (فاز ۲) — ۳ نوامبر** | مدل و دیتابیس بدون تغییر ماند و migration ساخته نشد. EF Core 10.0.11 ایندکس را از سمت Owner روی ستون‌های `Snapshot` نمی‌پذیرد (`OwnsOne`، `ComplexProperty` و نام متنی همه رد شدند)، و SQL خام طبق AGENTS.md ممنوع است. پس Shadow Property `BuyerId` می‌ماند. رفتار ایندکس با `BuyerCourseUniqueConstraintExistsInEfModelAndPostgresDatabase` (خطای `23505`) و `ConcurrentCartsForSameChapterHaveOnlyOneFinancialEffect` سبز است. |
| **اتصال فرانت به سفارش با کالا و رسید (I11-O، TRACK-03/04) — ۳ نوامبر** | ۱۰۷ آزمون Jest در ۹ مجموعه؛ تغییر وضعیت ارسال فقط از route سروری Next. پذیرش سرتاسری با JWT واقعی باقی است. |
| **سفارش و رسید با کالای فیزیکی I11-O — ۱ نوامبر** | migration `AddPhysicalDeliveryStatus` روی توسعه؛ ۴۳۹ موفق و ۲۰ آزمون `PostgresFact` بدون `BYEMONEY_TEST_CONNECTION_STRING` اجرا نشدند. |
| **ادغام و تغییر نام سبد؛ آزمون سبد روی master — ۳۱ اکتبر و ۱ نوامبر** | شاخه‌های `cart-storage-merge` و `cart-entity-rename` در master ادغام و push شدند (`9a8f865`)؛ سه migration (`MergeCartItemAllocation`، `RenameCartRequestSignatures`، `RenameCartTables`) روی schema تازه آزموده شدند. ۳۳ آزمون `CartPostgresTests` موفق؛ خرید رایگان با `MarkFree` به `Debited` می‌رود و خطای قبلی `MarkNotificationSent` برطرف است. |

## شواهد ۲۱ تا ۲۵ اکتبر (خلاصه)

- **خرید فصل، سبد مختلط، کالای فیزیکی (۲۱ و ۲۲ اکتبر):** فرانت، بای‌مانی و استرپی هر سه برش آزموده شدند: خرید فصل با نور (۱۰ آزمون Jest تازه)، تخصیص ۱۰۰ نور از پایهٔ ۱۲۰ با کوپن ۲۰ و تراز صفر Ledger، رقابت دو سبد یک فصل، migration `AddChapterCartPurchases`، migration استرپی `chapter-purchase` روی دادهٔ واقعی (۴۰ ردیف، صفر اختلاف نسخه‌ها)، رزرو اتمیک موجودی با ۱۰ سناریوی PostgreSQL و آزمون HTTP قیمت‌گذاری/ارسال/آدرس با JWT کنترل‌شده. بازنویسی کامل وبهوک قدیمی دوره (AUD-08) باقی است.
- **اصلاح‌های کوچک بک‌اند (۲۱ اکتبر):** شارژ ادمین با/بدون رسید با دو درخواست هم‌زمان تنها یک اثر مالی داشت؛ در Strapi 5.24.1 ایجاد سفارش از همهٔ مسیرها `currency=noor` ثبت کرد. ناهماهنگی‌های باقی‌مانده: فرانت `amountRial` می‌فرستد و بک `AmountToman` می‌پذیرد؛ مسیر دستی فرانت هنوز totalPrice را از ورودی می‌گیرد (تسک سفارش دستی).
- **بازبینی سبد کامل و پرداخت آزمایشی (۲۵ اکتبر):**
  - در آن اجرا یک آزمون `legacyMock.test.js:81` شکست خورد: در مغایرت مبلغ، callback صفحهٔ failed می‌دهد اما وضعیت سفارش Strapi تغییر نمی‌کند و فقط `notes` ثبت می‌شود. این مسیر خرید ریالی Legacy است و رفتار callback نوری را تأیید نمی‌کند.
  - بازتولید باگ تخصیص کوپن: دورهٔ ۱۰۰ و کالای ۲۰۰ با شناسهٔ عددی یکسان `1` و کوپن ۱۰٪ فقط برای دوره، `discount=40` داد به‌جای ۱۰. آزمون‌های سبز آن را پوشش نمی‌دادند؛ وضعیت رفع در HOTFIXES (HF06) پیگیری شود، نه اینجا.
  - **مغایرت سبد فیزیکی (تطبیق ۲ نوامبر):** سبد Paid/Synchronized دارای قلم Product بدون `PhysicalProductPurchase` در دیتابیس توسعه دیده شد. **حالت «ب»، دادهٔ تاریخی؛ تغییر کد نشد.** شاهد: `CartStore` همهٔ ردیف‌ها را در یک تراکنش ذخیره می‌کند، نوع `Product` با migration `20261022124021` آمد و پیش از آن پرداخت فقط `CoursePurchase` می‌ساخت. علت قطعی **اثبات‌نشده (تأییدنشده)** است: دیتابیس هنگام تطبیق خالی بود ؛ آزمون PostgreSQL پرداخت سبد Product از مسیر `CartStore` اکنون هست (`PhysicalCancellationPostgresTests`، `PhysicalDeliveryHttpPostgresTests`). در پرداخت دستی ۲۵ اکتبر ردیف فیزیکی درست ثبت شد.
- **پرداخت‌های دستی با SEP mock (۲۵ اکتبر، API و دیتابیس محلی، بدون مرورگر):**
  - سبد مختلط با کوپن (دورهٔ ۲۰۱ نور کاملاً با کوپن پوشش داده شد؛ مبلغ پرداختی ۲٬۵۰۰٬۰۰۰ نور) ثبت شد: `Verified`، TopUp `Confirmed`، سبد `Paid`، دو `CoursePurchase` و یک `PhysicalProductPurchase`، موجودی ۳ به ۲، Ledger متوازن و پرداخت مجدد همان سبد بدون خرید تکراری.
  - `bank_failed` و `amount_mismatch` با برگشت موفق: بدون Ledger و خرید؛ سبد `Failed`.
  - `reverse_failed`: TopUp `Pending/ReverseFailed` و `isUnderReview=true`؛ سبد `AwaitingTopUp` عمداً باز ماند و نیازمند رسیدگی دستی مالی است.
  - نشانی ناقص: رزرو با `CART_INVALID` رد شد.

## شواهد ۱۷ تا ۲۰ اکتبر (خلاصه)

- **پذیرش مرورگر با SEP mock (۱۸ اکتبر):** مرورگر واقعی و سه سرویس محلی؛ پوشش: پیش‌نمایش و کوپن، خرید رایگان، شارژ مستقل، انصراف و تلاش دوباره، مغایرت مبلغ (`ReverseSucceeded` و `ReverseFailed/financial_review`)، نتیجهٔ نامعلوم (رسید منقضی، رسیدگی مالی حفظ شد) و دو دوره با کوپن (۳۵۲ از ۷۰۴، تخصیص ۲۵۲ و ۱۰۰، callback تکراری بدون مصرف دوبارهٔ کوپن). دادهٔ تست و پرونده‌های نامعلوم/Reverse شکست‌خورده برای پیگیری باقی ماندند.
- **سبد دوره و شارژ (۱۷ و ۱۸ اکتبر):** تک‌دوره و شارژ کسری، سبد چنددوره با کوپن، خرید خودکار پس از شارژ مستقل از مرورگر (شکست خرید ماندهٔ شارژشده را حفظ کرد)، لغو و تازگی سبد، پرداخت مجدد همان سبد، پیش‌نمایش بدون رکورد و بدون مصرف کوپن، اقلام سبد رابطه‌ای و تخفیف سطح سبد (migrationهای مربوط روی توسعه؛ دادهٔ سبد آزمایشی با اجازهٔ مالک پاک شد؛ خرید، Wallet و Ledger حفظ شدند).
- **I11-P و I11-T و رسید ادمین (۱۹ و ۲۰ اکتبر):** فهرست خرید، رسید، پیگیری و رسید ادمین با HTTP کنترل‌شده، مالکیت، مجوز و صفحه‌بندی آزموده شد. migrationهای `AddCoursePurchaseReferenceRateSnapshot` (نرخ nullable، بدون backfill) و `AddPurchasesViewPermission` روی توسعه اعمال شدند. شاهد ۲۰ اکتبر: ۳۲۹ موفق، صفر شکست.
- **بازسازی ساختاری (۲۰ اکتبر):** مدل، migration و قرارداد HTTP تغییر نکردند. انتقال منطق مالی Store به Application انجام نشده و CLEANUP-01 باز است.

## شواهد قدیمی مرتبط با پذیرش باز (تا ۱۷ اکتبر)

| موضوع | نتیجه و محدودیت |
|---|---|
| نقش و مجوز (۵ اکتبر) | آزمون ماتریس نقش/مجوز؛ بدون JWT واقعی یا دیتابیس. |
| رسیدگی و بازپرداخت دستی (۶ اکتبر) | migration `ResolveReviewedTopUpStatuses` روی توسعه؛ `TR-6PE2FSD2` در DB/API برابر ManuallyRefunded. انتشار اثبات نشد. |
| Wallet و بازیابی شارژ (۷ و ۸ اکتبر) | migration `ProtectWalletConcurrentBalance` روی توسعه؛ بدون بانک واقعی. |
| قیمت سروری R02 و هات‌فیکس قیمت master (۸ تا ۱۵ اکتبر) | آزمون‌های بک، فرانت و ۲ سناریوی PostgreSQL موفق. ادغام هات‌فیکس در master با `523b58e` و `d9e497e` ثبت شد؛ انتشار و پذیرش HF01 باقی است. آزمون اصلی قیمت: بک `tests/api/integration/getCourse.test.js`. |
| NEXT-01 و NEXT-02 (۱۵ اکتبر) | اصلاح قیمت با ۵۳ Jest و ۱۰ بررسی مستقل موفق. بررسی اول قبولی .NET نداشت. |
| کوپن HF15 و گردکردن checkout (۱۵ تا ۱۷ اکتبر) | ۱۲ مقایسهٔ فرانت/بک برابر؛ ۳ شکست کل مجموعهٔ فرانت روی مبنا هم تکرار شد. پذیرش واقعی و انتشار جداست. |
| دسترسی توکن تلاش پرداخت (۱۷ اکتبر) | افزودن find/create/update به توکن محلی NEXTJS؛ GET از ۴۰۳ به ۲۰۰. مقصد انتشار جدا بررسی شود. |

## RBAC-01 — حذف پیش‌چک موجودی از ثبت دستی سفارش (فرانت) — ۸ نوامبر ۲۰۲۶

- تغییر: `POST /api/admin/orders/manual` دیگر `batch-balances` را صدا نمی‌زند؛ کفایت موجودی فقط با خطای بای‌مانی (`insufficientBalance`) اعلام می‌شود. بدون تغییر بک‌اند، API عمومی یا مالی.
- آزمون: فقط بررسی نحو (`node --check`) انجام شد؛ برای این route آزمون خودکار وجود ندارد و ESLint در پروژه پیکربندی ندارد. پذیرش دستی مرورگر (۸ نوامبر ۲۰۲۶): ادمین بدون `Wallet.View` با `Courses.Manage` خرید پولی را بدون خطای کاذب انجام داد؛ تأیید مالک پروژه.

## RBAC-02 — موجودی نامشخص به‌جای ۰ در پنل ادمین (فرانت) — ۸ نوامبر ۲۰۲۶

- تغییر: شکست یا نبود دسترسی استعلام `batch-balances` در `/admin/users` و فرم سفارش دستی دیگر ۰ یا `light` قدیمی نشان نمی‌دهد؛ «نامشخص» با توضیح نبود `Wallet.View` نمایش داده می‌شود و فقط وقتی موجودی معلوم و کم است دکمهٔ ثبت غیرفعال می‌شود (کفایت نامشخص را ByeMoney در خرید بررسی می‌کند). جمع محلی پس از شارژ برای موجودی نامشخص انجام نمی‌شود.
- آزمون: Jest `src/components/admin`، `src/lib/admin`، `src/app/api/admin` ۲۴ مجموعه و ۲۴۱ آزمون موفق. پذیرش دستی مرورگر (۸ نوامبر ۲۰۲۶): ادمین بدون `Wallet.View` موجودی «نامشخص» دید و دکمهٔ ثبت فعال بود؛ ادمین کامل بدون تغییر؛ تأیید مالک پروژه.

## SHIP-TRACKING — کد رهگیری، تحویل خودکار و فیلتر «ارسال‌نشده» — ۱۴ نوامبر ۲۰۲۶

- تغییر: ستون‌های nullable `TrackingCode`/`TrackingProvider`/`ProviderParcelId` روی `PhysicalProductPurchases` با migration افزودنی `AddPhysicalShipmentTracking`؛ `PUT /api/admin/physical-purchases/{id}/delivery-status` فیلدهای اختیاری کد/منبع/شناسهٔ مرسوله را می‌گیرد (اصلاح کد در Shipped، قفل پس از Delivered)؛ اندپوینت خریدار `POST /api/me/orders/{orderId}/confirm-delivery`؛ فیلتر سروری `awaitingShipment=true` در `GET /api/admin/orders`؛ `PhysicalDeliverySyncService` با همگام‌سازی وضعیت پستکس (هدر `x-api-key`، فقط با `Postex:Enabled` و کلید) و مهلت پشتیبان ۱۴ روزه؛ فرانت: ثبت/نمایش کد و دکمهٔ تأیید خریدار و حذف اسکن ۱۰۰تایی تب «نیاز به اقدام».
- آزمون: بای‌مانی ۵۲۹ موفق و ۱۲۰ Skip (Skip همان آزمون‌های دیتابیسی مشروط؛ ۱۶ آزمون تازه: دامنهٔ کد رهگیری، تفسیر پاسخ پستکس، فیلتر awaitingShipment). Jest فرانت کامل: ۱۱۱ مجموعه و ۱۱۴۸ آزمون موفق. `next build` موفق و مسیر `/api/orders/[orderId]/confirm-delivery` در خروجی build ثبت شد. migration روی `ByeMoney_Dev` اعمال و `Down`/`Up` دوباره موفق.
- محدودیت: فراخوانی واقعی پستکس انجام نشد (کلید نیست)؛ شکل پاسخ `parcels/{parcel-no}/status` در OpenAPI پستکس schema ندارد و `PostexStatusParser` تحمل‌گر نوشته شده و باید با یک مرسولهٔ واقعی تأیید شود؛ آزمون‌های PostgreSQL اختیاری (`BYEMONEY_CART_PG_TESTS=1`) اجرا نشد؛ پذیرش مرورگر با JWT واقعی باقی است.

## SHIP-TRACKING-C — عمومی‌سازی واسط پستی — ۱۵ نوامبر ۲۰۲۶

- تغییر: پورت عمومی `ITrackingProviderClient` جای پورت پستکسی را گرفت و `SyncExternalAsync` واسط هر قلم را از منبع همان قلم انتخاب می‌کند؛ `ExternalTrackingBackgroundService` همهٔ واسط‌های ثبت‌شده را می‌گرداند؛ `GetShippedAsync` فهرست کلید منبع می‌گیرد؛ `AutoConfirmAfterDays` و `PollIntervalMinutes` به بخش `PhysicalDelivery` رفت؛ `ProviderParcelId` متنی شد و قید منبع با migration `GeneralizeShipmentTracking` بازنویسی شد؛ ثبت قلم با منبع خودکارِ بدون شناسهٔ ارجاع رد می‌شود؛ متن خطاها بی‌نام فروشنده و فهرست منابع فرانت از یک ماژول می‌آید.
- آزمون: `dotnet build` روی `ByeMoney.UnitTests` موفق (۰ خطا، ۰ اخطار)؛ `dotnet test` کامل ۵۴۵ موفق و ۱۲۰ Skip و ۰ ناموفق، شامل ۳۲ آزمون `ShipTrackingTests` و `PhysicalDeliverySyncTests`. فرانت: `PhysicalDeliveryControl.test.jsx` (۶ موفق) و `byeMoneyPhysicalOrders.test.js` (۳۴ موفق).
- محدودیت: migration `GeneralizeShipmentTracking` روی `ByeMoney_Dev` (با ۱۴ ردیف داده) اعمال و سپس `Down`/`Up` دوباره اجرا شد و ستون به `character varying(50)` رسید؛ نگاشت وضعیت‌های خام پستکس تغییر نکرد و همچنان با مرسولهٔ واقعی تأیید نشده است؛ سه سناریوی Swagger و اصلاح کد روی قلم ارسال‌شده در مرورگر باقی است.

## SHIP-TRACKING-B + پارسر واقعی — ۱۵ نوامبر ۲۰۲۶

- تغییر: `PostexStatusParser` فقط با `parent.code`/`status_code` تصمیم می‌گیرد (استثناهای ۱۱۳۱۱ و ۱۱۳۱۰ در مسیر می‌مانند). پورت عمومی `IShipmentProviderClient` و پیاده‌سازی `PostexShipmentClient` (اول خواندن `custom-order-no`، بعد `bulk`؛ مبدأ رامسر city_id=۵۷۳؛ نگاشت شهر گیرنده از `cities/all`). فرمان/اندپوینت ادمین `POST /api/admin/physical-purchases/{id}/shipments`؛ فرانت دکمهٔ «ثبت مرسوله در واسط پستی». انتخاب هزینهٔ ثابت/پستکس اسلایس جدا (`SHIP-COST-MODE`).
- آزمون: `dotnet test` کامل ۶۶۵ موفق و ۰ ناموفق (شامل `ShipTrackingTests`، `CreatePhysicalShipmentTests`، `PhysicalDeliverySyncTests`). فرانت `PhysicalDeliveryControl.test.jsx` ۷ موفق. استعلام زندهٔ مرسولهٔ تستی با کلید Development: HTTP ۲۰۰ و وضعیت `PAYMENT_PENDING` → InTransit.

## SHIP-TRACKING — پذیرش زندهٔ P3 (B/C) — ۲۴ نوامبر ۲۰۲۶

- محیط: `ByeMoney_Dev` روی PostgreSQL محلی؛ API `http://localhost:5000`؛ JWT امضاشده با همان `Strapi:Jwt*` اجرای API (نه توکن مرورگر استرپی).
- آزمون خودکار (پیش‌نیاز): فیلتر `ShipTrackingTests|PhysicalDeliverySyncTests|CreatePhysicalShipmentTests|PhysicalDeliveryHttpPostgresTests` با `BYEMONEY_CART_PG_TESTS=1` → ۸۳/۸۳ سبز.
- ساخت مرسولهٔ سطح سبد: `POST /api/admin/carts/{cartId}/shipments` روی سبد پرداخت‌شده با قلم `Preparing` و عوامل مرسوله کامل → HTTP ۲۰۰، `outcome=PendingPayment`، `providerParcelId=9020373643864` (کیف پستکس ناکافی؛ رفتار SHIP-FIX-1).
- پستکس زنده: استعلام `parcels/9020373643864/status` → HTTP ۲۰۰؛ `PostexStatusParser` → `InTransit` (انتظار پرداخت).
- Swagger: `PUT delivery-status` خرید ناموجود → ۴۰۴؛ `Shipped` بدون کد → ۴۰۰؛ `POST shipments/refresh` → ۲۰۰؛ `GET orders?awaitingShipment=true` → ۲۰۰.
- خریدار: `POST /api/me/orders/cart:{cartId}/confirm-delivery` با JWT خریدار پس از `Shipped` دستی با کد `TRACK-LIVE-24` → HTTP ۲۰۰، `confirmedCount=1`.
- باقی برای بستن ردیف TASKS: نمایش مرورگر (فرانت `localhost:3000` در این اجرا خطای ۵۰۰ روی `/profile/orders`)؛ همگام‌سازی خودکار `Delivered` از پستکس وقتی مرسوله واقعاً تحویل‌شده و بارکد/وضعیت قطعی باشد (پس از شارژ کیف پستکس برای مرسولهٔ `9020373643864`).

## SHIP-BOX-TYPE — جعبهٔ استاندارد پستکس — ۱۸ نوامبر ۲۰۲۶

- تغییر (استرپی): فیلد `boxTypeId` (enumeration جعبه‌های ۱…۱۲ با نام و ابعاد)؛ حذف `length`/`width`/`height` از کالا؛ جدول ثابت `postexBoxes`؛ انتشار بدون جعبه با middleware رد می‌شود؛ استعلام و prepare با `box_type_id`+ابعاد مشتق؛ نبود جعبه → «جعبهٔ کالا تعیین نشده» (+ مهاجرت `2026.11.18T12.00.00.product-box-type`).
- تغییر (بای‌مانی): `boxTypeId` در قرارداد prepare و `ParcelFacts`؛ ستون nullable `ParcelBoxTypeId` + قید همه‌یا‌هیچ (migration `20261118061509_AddParcelBoxTypeId`؛ عوامل قدیمی بدون جعبه در Up خالی شد)؛ `parcels/bulk` با `box_type_id`.
- اسناد: D08، I09-PH، Postex_API بندهای ۴.۱/۵.۱/۸.۲.۴ و شاهد ۱۰.۱.
- آزمون: Jest استرپی `postexBoxes`+`cartShipping`+`postexShippingQuote` — ۱۷ موفق. بای‌مانی فیلتر parcel/shipment/cart pricing — ۸۴ موفق. استعلام زنده: بدون جعبه ۴۰۰؛ با جعبهٔ ۵ مبلغ `3166000`.
- محدودیت: پذیرش Swagger prepare/ساخت مرسوله و انتخاب جعبه در Content-Manager باقی است؛ `parcels/bulk` زنده زده نشد. فرانت ویترین/ادمین Next این فیلد را ویرایش نمی‌کند.

## SHIP-PARCEL-FACTORS — عوامل واقعی مرسوله — ۱۷ نوامبر ۲۰۲۶

- تغییر (استرپی): فیلدهای `length`/`width`/`height`/`isFragile` روی کالا (+ مهاجرت `2026.11.17T12.00.00.product-parcel-factors`)؛ `weight` موجود گرم است. در `postex` هر قلم جدا استعلام می‌شود با وزن×تعداد، ابعاد واقعی، ارزش ریالی قلم، `is_fragile` کالا، بدون `box_type_id`؛ هزینهٔ سبد جمع استعلام‌هاست؛ نبود وزن/ابعاد → تعارض. کف ۵۰ میلیون فقط پس از رد پستکس. خطای اعتبارسنجی پستکس (`errors`/`invalid_fields`) در پیام تعارض می‌آید. prepare روی ردیف کالا `parcel` با عوامل فریزشدنی برمی‌گرداند. این ردیف شاهد تاریخ ۱۷ نوامبر است، نه قاعدهٔ جاری؛ از SHIP-BOX-TYPE (بالاتر) استعلام تجمیعی سبد با جعبهٔ استاندارد و بدون کف جای آن را گرفت.
- تغییر (بای‌مانی): `parcel` در قرارداد prepare به `ParcelFacts` نگاشت و روی `CartItems` و `PhysicalProductPurchases` (ستون‌های nullable + قید همه‌یا‌هیچ) فریز شد (migration `20261117105330_AddShipmentParcelFactors`). بدنهٔ `parcels/bulk` فقط اعداد فریزشده را با `parcel_items` می‌فرستد؛ `box_type_id`، ۲۵۰ گرم و ۵۰ میلیون حذف شدند. `pick_up` بدون `lat`/`lon` مبدأ پیش از bulk با پیام روشن رد می‌شود و `ExtractErrorMessage` خطاهای `errors`/`invalid_fields` را نشان می‌دهد.
- اسناد: I09-PH، بند هزینه ارسال D و Postex_API (بندهای ۸.۲.۲، ۸.۲.۴ و ۸.۲.۷) به‌روز شد.
- آزمون: Jest استرپی `cartShipping` + `postexShippingQuote` — ۱۶ موفق؛ مجموعهٔ `tests/services` + `physicalCartNoOrder` — ۷۱ موفق. بای‌مانی: `dotnet test` کامل ۵۷۵ موفق، ۱۲۱ Skip، ۰ ناموفق؛ `CartPostgresTests` با `BYEMONEY_CART_PG_TESTS=1` — ۳۹/۳۹ موفق (شامل ماندگاری parcel روی سبد و خرید)؛ migration روی دیتابیس توسعه اعمال و Down/Up دوباره اجرا شد.
- محدودیت: مختصات واقعی انبار رامسر در تنظیمات نیست و `Lat`/`Lon` خالی مانده؛ تا تکمیل آن، `pick_up` عمداً با پیام روشن متوقف می‌شود. پذیرش ساخت مرسوله با کلید زنده و بارکد واقعی باقی است.

## SHIP-COST-MODE — هزینهٔ ثابت یا برآورد پستکس — ۱۵ نوامبر ۲۰۲۶


- تغییر (استرپی): `shipping-setting` فیلدهای `costMode` (`fixed`|`postex`) و `rialPerNoorForShipping`؛ `cartShipping` در mode پستکس از `postexShippingQuote` با `POST /api/v1/shipping/quotes` (مبدأ رامسر ۵۷۳، کوریر `IR_POST`/`EXPRESS`) مبلغ `total_cost` را می‌گیرد، به نور صحیح سقف می‌کند و آستانهٔ رایگان را پیش از فراخوانی اعمال می‌کند؛ بدون کلید یا خطای پستکس → `CART_CONFLICT`. بای‌مانی و فرانت قرارداد `shippingAmountInNoor`/`shippingRuleLabel` را عوض نکردند.
- آزمون: Jest `tests/services/cartShipping.test.js` و `postexShippingQuote.test.js` — ۹ موفق.
- محدودیت: استعلام زنده با کلید واقعی و پذیرش سبد فیزیکی در mode پستکس باقی است؛ چند مبدأ در SHIP-MULTI-ORIGIN (انجام‌شده؛ مبدأ ثابت رامسر برداشته شد).

## SHIP-MULTI-ORIGIN — چند مبدأ/انبار ارسال — ۱۶ نوامبر ۲۰۲۶

- تغییر (استرپی): نوع محتوای `shipping-origin`؛ فیلد رابطه‌ای `shippingOrigin` روی کالا؛ `resolveCartOrigin` در prepare؛ quote پستکس `from_city` را از مبدأ کالا می‌گیرد؛ پاسخ prepare فیلدهای `shippingOriginId`/`shippingOriginLabel`؛ bootstrap مبدأ پیش‌فرض `ramsar`؛ چند مبدأ در یک سبد → `CART_CONFLICT`.
- تغییر (بای‌مانی): ستون nullable `ShippingOriginId` روی `Carts`؛ فریز از prepare؛ `Postex:Origins` keyed (با سازگاری `Sender`→`ramsar`)؛ ساخت مرسوله پروفایل همان مبدأ فریزشده را می‌فرستد؛ نبود مبدأ → خطای صریح. فرانت تغییری نکرد (Content-Manager کافی است).
- اسناد: I09-PH، D08/هزینه ارسال، Postex_API هم‌تراز شد.
- آزمون: Jest استرپی `cartShipping` + `postexShippingQuote` + `shippingOrigins` — ۱۵ موفق. بای‌مانی (با `-o .artifacts/ship-multi-origin-test` به‌خاطر قفل DLL توسط API در حال اجرا): `CartPricingClientTests` + `CreatePhysicalShipmentTests` + `CartTests` — ۴۲ موفق ۰ ناموفق. migration `AddCartShippingOriginId` روی دیتابیس توسعه اعمال شد.
- محدودیت: پذیرش دستی تخصیص مبدأ به کالا و مرسولهٔ واقعی در TASKS بخش «نیازمند تست دستی».

## SHIP-ORIGIN-CITY-TABLE — استان/شهر و مبدأ دراپ‌داون — ۱۶ نوامبر ۲۰۲۶

- تغییر (استرپی): نوع‌های `province`/`city` با `externalId` (بدون برند حامل در دامنه/پنل)؛ آداپتور همگام‌سازی فعلی پستکس (`postexLocalitySync` + `POST /api/cities/sync-localities`)؛ `shipping-origin` فقط رابطه به شهر (بدون اسنپ‌شات cityId/cityName)؛ quote از `city.externalId`؛ کالای بدون مبدأ → رامسر؛ چند مبدأ تعارض؛ برچسب فارسی بدون نام حامل. ByeMoney و front-end تغییر نکردند.
- گام ۲ بعداً جدا اجرا شد (بخش بعدی).
- اختلاف سند: متن قبلی SHIP-MULTI-ORIGIN «نبود مبدأ روی کالا = تعارض» می‌گفت؛ رفتار کد اکنون پیش‌فرض رامسر است.
- آزمون: Jest `shippingOrigins` + `postexLocalitySync` + `shippingCmLabels` + `cartShipping` + `postexShippingQuote` — ۲۱ موفق (پس از اصلاح `externalId` و حذف اسنپ‌شات مبدأ).

## BUYER-CITY-LINK — اتصال آدرس به شناسهٔ شهرِ خودمان — ۱۶ نوامبر ۲۰۲۶

- تغییر (استرپی): `GET /api/localities` شهر را با `documentId` و نام می‌دهد و `externalId` نمی‌دهد؛ ذخیرهٔ آدرس فقط با اتصال `cityLink` به همان ردیف است و نام استان/شهر از ردیف کپی می‌شود؛ `addressSnapshot` فیلد `cityDocumentId` دارد و کد حامل ندارد؛ برآورد پستکس `to_city_code` را از `externalId` همان ردیف می‌خواند؛ `POST /api/integrations/byemoney/v1/localities/carrier-code` فقط با کلید سرویس کد حامل را برمی‌گرداند (برای سفارش قدیمی با نام دقیق). آدرس بدون اتصال برای سبد فیزیکی رد می‌شود.
- ByeMoney: فریز `cityDocumentId` و خواندن کد حامل در بخش FREEZE-CITY-DOCUMENT انجام شد. فرانت از قبل فقط نام آدرس را نشان می‌دهد.
- آزمون: Jest روی localities، lifecycle آدرس، مسیر localities، carrier-code، cartShipping، physicalAddress، postexShippingQuote و shippingCmLabels — ۳۶ موفق.

## FREEZE-CITY-DOCUMENT — فریز شناسهٔ شهر و خواندن کد حامل — ۱۶ نوامبر ۲۰۲۶

- تغییر (بای‌مانی): `ShippingAddress.CityDocumentId` روی سبد فریز می‌شود (مهاجرت افزودنی nullable). prepare بدون این شناسه برای سبد کالا نامعتبر است. ساخت مرسوله کد حامل را از `POST /api/integrations/byemoney/v1/localities/carrier-code` می‌خواند: با شناسه، یا برای سفارش قدیمی فقط با نام دقیق استان و شهر. کد ذخیره نمی‌شود و `api/v1/locality/cities/all` دیگر برای گیرنده خوانده نمی‌شود. شهرِ پیدا نشده یا قطع خواندن، مرسوله نمی‌سازد. جزئیات خریدار و ادمین همان نام‌ها را برمی‌گردانند. محاسبهٔ هزینه، مبدأ و `Postex:Origins` عوض نشد. فرانت تغییر نکرد.
- آزمون: همان پنج مجموعه ۴۰ موفق و ۰ ناموفق (خروجی جدا به‌خاطر قفل DLL توسط API در حال اجرا). migration `AddCartShippingCityDocumentId` روی دیتابیس توسعه اعمال شد؛ ستون `ShippingAddress_CityDocumentId` تهی‌پذیر با طول ۶۴ است.
- محدودیت: مرسولهٔ زنده با کلید پستکس و سفارش قدیمی واقعی در این اجرا نبود.

## BUYER-ADDRESS-LOCALITY-DROPDOWN — آدرس خریدار از جدول استان/شهر — ۱۶ نوامبر ۲۰۲۶

- تغییر (استرپی): `GET /api/localities` فقط‌خواندنی با `auth: false` → `{ data: [{ name, cities: [name, ...] }] }` (مرتب، بدون id/`externalId`، بدون صفحه‌بندی)؛ سرویس `localities`؛ lifecycle آدرس `beforeCreate`/`beforeUpdate` فقط جفت رسمی استان/شهر را بعد از trim می‌پذیرد. رابطهٔ شهر روی `address` اضافه نشد.
- تغییر (فرانت): پروکسی نشست‌دار `GET /api/localities`؛ کنترل مشترک `ProvinceCitySelects` در `AddressModal` و `ProfileForm`؛ payload همچنان رشته‌های `province`/`city`.
- ByeMoney: بدون تغییر؛ شکل `addressSnapshot` حفظ شد.
- آزمون: استرپی Jest سه فایل localities/lifecycle/route — ۱۵ موفق. فرانت Jest مدال آدرس + کنترل مشترک + ProfileForm + route localities — ۱۷ موفق.
- محدودیت: تا `sync-localities`/bootstrap جدول پر نشود فرم ذخیره نمی‌کند؛ آدرس قدیمی با املای غیررسمی باید دوباره از لیست انتخاب شود؛ حفرهٔ نبود بررسی نشست روی `/api/addresses` خارج از دامنه ماند.

## ORDER-ORIGIN-DISPLAY — نمایش آدرس و مبدأ از بای‌مانی — ۱۶ نوامبر ۲۰۲۶

- تغییر (بای‌مانی): ستون nullable `ShippingOriginLabel` روی `Carts`؛ فریز برچسب کنار شناسه از prepare؛ پاسخ سبد `shippingOrigin` و `shippingAddress` (بدون شناسهٔ شهر)؛ جزئیات ادمین همیشه و جزئیات خریدار فقط برای سبد `Paid` همان مبدأ را برمی‌گردانند. سفارش قدیمی بدون برچسب فقط شناسه دارد.
- تغییر (فرانت): خلاصهٔ سبد، جزئیات سفارش خریدار و پنل/کشوی ادمین نشانی را با استان و شهر نشان می‌دهند و مبدأ را از پاسخ بای‌مانی (یا برچسب پیش‌نمایش، پیش از ساخت سبد) می‌نویسند.
- آزمون: بای‌مانی `CartTests` + `CartPricingClientTests` + `CartRefreshTests` — ۵۰ موفق. فرانت Jest همین مسیر (سبد، سفارش خریدار، پنل ادمین، route سفارش و tracking) — ۱۰۹ موفق. migration `AddCartShippingOriginLabel` روی دیتابیس توسعه اعمال شد.
- محدودیت: مرورگر با نشست واقعی در این اجرا باز نشد.

## SHIP-CREATE-ONCE و SHIP-UNPAID — مرسولهٔ تکراری و پرداخت‌نشده — ۱۶ نوامبر ۲۰۲۶

- تغییر (بای‌مانی): خواندن `custom-order-no` فقط با ۴۰۴ «نیست» است؛ خطای خواندن یا بدنهٔ بدون `parcel_no` ساخت را شروع نمی‌کند. فراخوانی واسط داخل قفل خریدار/سبد است. والد ۱ / کد ۱۰۰۰۰ / `PAYMENT_PENDING` و پاسخ ۴۰۲ خرید را `Shipped` نمی‌کند و ۴۰۰ با پیام دامنه برمی‌گرداند؛ تسویه چون هنوز `Preparing` است باز نمی‌شود. دکمهٔ پنل همان مسیر خطای ۴۰۰ را نشان می‌دهد. فرانت عوض نشد.
- اسناد: بند ۸.۲ `Postex_API` و جملهٔ ساخت مرسوله در I11-O.
- آزمون: `CreatePhysicalShipmentTests` + `PostexShipmentCityCodeTests` + `OrderSettlementTests` — ۳۹ موفق و ۰ ناموفق.
- محدودیت: پستکس زنده صدا زده نشد. مرسولهٔ پرداخت‌نشدهٔ قبلی با کلیک دوباره پرداخت نمی‌شود؛ تا وضعیت واسط از «در انتظار پرداخت» خارج شود، ساخت دوم انجام نمی‌شود و خرید `Preparing` می‌ماند. قفل سبد تا پایان مهلت پستکس (پیش‌فرض ۲۰ ثانیه) نگه داشته می‌شود.

## هم‌ترازی تکرار `operationId` در `I09-PC` — ۲۵ نوامبر ۲۰۲۶

- تغییر (بای‌مانی): مقایسهٔ `reason` trim‌شده در بازپخش `operationId` لغوهای پرداخت‌شده (خریدار و ادمین)؛ افزودن موجودیت و جدول `PhysicalReservationCancellations` (مهاجرت افزودنی `AddPhysicalReservationCancellations` با ایندکس یکتا روی `OperationId` و ایندکس روی `CartId`) برای ثبت پایدار لغو پرداخت‌نشده پیش از فراخوانی آزادسازی؛ جست‌وجوی `operationId` در هر دو منبع پیش از تصمیم بر اساس وضعیت سبد و زیر قفل خریدار و سبد؛ بازپخش هوشمند لغو پرداخت‌نشده بر پایهٔ `ReservationId` فعلی (تکرار آزادسازی در همان رزرو با حفظ ۵۰۳، و پاسخ ۲۰۰ بدون دست‌زدن به رزرو تازه یا Ledger در بازگشایی/پرداخت).
- اسناد: بخش‌های `I09-C` و `I09-PC` در `ByeMoney-Strapi-Integration.md` به‌روزرسانی شدند.
- آزمون: `dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj -c Release` — ۸۳۴ موفق، ۰ ناموفق، ۰ ردشده (شامل ۵ آزمون جدید در `PhysicalCancellationPostgresTests` برای تعارض دلیل/سبد، تلاقی پرداخت‌نشده و پرداخت‌شده در مسیر خریدار و ادمین، بازگشایی و پرداخت پس از لغو پرداخت‌نشده، دو لغو متوالی، شکست ۵۰۳ و تکرار، و رقابت هم‌زمان روی PostgreSQL).
- محدودیت: بیلد و آزمون در پیکربندی `Release` اجرا شد (پیکربندی `Debug` به دلیل قفل فایل توسط فرایند در حال اجرای `ByeMoney.API` در دسترس نبود) و مهاجرت جدید هنوز روی دیتابیس توسعهٔ برنامهٔ در حال اجرا اعمال نشده است.

## هم‌ترازی تکرار `evidenceId` در بازگشایی رسیدگی درگاه `I07` — ۲۵ نوامبر ۲۰۲۶

- تغییر (بای‌مانی): در `TopUpRequest.ReopenFinancialReview` و `GatewayTopUpService.ReopenReviewAsync` تطبیق `EventType == "evidence"` و `note` برای تکرار `evidenceId` اضافه شد تا تکرار یکسان `200` بدهد و تغییر `note` یا استفاده از شناسهٔ عملیات دیگر تعارض `409` برگرداند (به همراه مدیریت هم‌زمانی).
- آزمون: `dotnet test src/ByeMoney.UnitTests/ByeMoney.UnitTests.csproj -c Release --filter "FullyQualifiedName~ManualGatewayReviewTests" --no-restore` — ۲۱ موفق، ۰ ناموفق، ۰ ردشده.
- محدودیت: در پیکربندی `Release` اجرا شد (فایل‌های `Debug` توسط فرایند در حال اجرای `ByeMoney.API` قفل بود).

## هم‌ترازی کد خطای رسید نامعتبر در خروج نور `I11-W` — ۲۵ نوامبر ۲۰۲۶

- تغییر (قرارداد اتصال): در متن عمومی `I11-W` تصریح شد که خطای تصویر رسید نامعتبر (`400 NOOR_WITHDRAWAL_INVALID_RECEIPT`) استثنای شکل عمومی اعتبارسنجی است و در قالب `{code, error}` برمی‌گردد؛ این مورد با جدول مسیرهای `I11-W`، کد کنترلر و مصرف‌کنندهٔ فرانت هم‌خوان است و تغییری در کد نیاز نداشت.
