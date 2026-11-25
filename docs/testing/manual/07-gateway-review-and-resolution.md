# ۰۷ — رسیدگی مالی درگاه: شارژ معلق، پروندهٔ باز و نتیجه‌های بستن آن

> دامنه: شارژ معلق درگاه (`Pending`)، استعلام تأیید/جزئیات پرونده، باز کردن پروندهٔ رسیدگی مالی (`open`)، بستن پرونده با نتیجه‌های `NO_MATCHING_DEPOSIT`، `MANUAL_REFUND`، `PAID_AND_CONFIRMED`، `REVERSED_REJECTED` و `CONFIRMED_EXCEPTIONAL`، بازگشایی پرونده (`reopen`) و کنترل نسخه، حفاظت دائمی پس از بازپرداخت دستی، تکرار درخواست (Idempotency) و خطاهای احراز هویت/مجوز/اعتبارسنجی/تعارض. قالب: [README](README.md).
> معیارها از I03، I07 (بخش‌های ۱ تا ۶) در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، [ماتریس دسترسی](../../architecture/Permissions-Matrix.md) و ردیف‌های B2، B7، E3 و E4 در [TASKS](../../../TASKS.md).

## پیش‌نیاز

- سرویس‌های PostgreSQL (`:5432`)، استرپی (`:1337`) و بای‌مانی (`:5000`) بالا باشند.
- `.\seed.ps1 -Action up` اجرا شده باشد (کاربران نمونه و نقش `Admin` با مجوز `TopUp.Review`).
- `.\seed.ps1 -Action gateway-review` اجرا شده باشد. این مرحله ۵ درخواست شارژ روی `gatewayReviewBuyer` (در صورت نبود دستهٔ `buyers`، روی `09120000002`) از مسیر API می‌سازد و شناسه‌ها را در `gatewayReview.items` ثبت می‌کند.
- برای اجرای دوبارهٔ کامل سناریو روی پرونده‌های تازه، یک دستهٔ تازهٔ `buyers` بسازید و دوباره `-Action gateway-review` را اجرا کنید (یا پس از `reset` و `up` دوباره `gateway-review` بزنید).

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` زیر `gatewayReview.items` بردارید، حدس نزنید):

| نماد | کلید در `gatewayReview.items` | وضعیت اولیه در سید | کاربرد در سناریو |
|---|---|---|---|
| `G_PEND` | `pendingTopUp` | `Pending` (۱۰ نور، بدون پرونده) | بخش‌های الف، ب، ج، د (معرفی پرونده، `NO_MATCHING_DEPOSIT`، `reopen`، `MANUAL_REFUND` و حفاظت دائمی) |
| `G_PAID` | `paidCaseTopUp` | `Confirmed` + پروندهٔ باز (`VERIFY_UNKNOWN`) | بخش هـ (`PAID_AND_CONFIRMED`، خطای `MANUAL_REFUND` روی `Confirmed`) |
| `G_REV` | `reversedCaseTopUp` | `Rejected` + پروندهٔ باز (`REVERSE_UNKNOWN`) | بخش و (`REVERSED_REJECTED`، خطای `CONFIRMED_EXCEPTIONAL` پس از برگشت بانکی) |
| `G_EXC` | `exceptionalCaseTopUp` | `Rejected` + پروندهٔ خودکار (`BANK_CONFLICT` با `Verified`) | بخش ز (`CONFIRMED_EXCEPTIONAL`، خطای اختلاف مبلغ، شارژ اتمیک بدون خرید خودکار، تکرار) |
| `G_C2C` | `cardToCardTopUp` | `Pending` (کارت‌به‌کارت، ۵ نور) | بخش ح (خطای روش پرداخت غیردرگاه) |

**قرارداد اجرا:**

- توکن JWT کاربر از `POST http://localhost:1337/api/auth/password/login` گرفته می‌شود.
- برای مسیرهای `/api/integrations/topups/*` (به‌جز `resolve`) هدر `X-Service-Key` با مقدار `STRAPI_TO_BYEMONEY_SERVICE_KEY` یا `FRONTEND_TO_BYEMONEY_SERVICE_KEY` (حداقل ۳۲ بایت؛ از `backend/.env` یا `appsettings.Development.json` بخوانید، در سند ننویسید) لازم است.
- مسیرهای `POST /api/admin/topups/gateway-reviews/resolve` و `POST /api/integrations/topups/v1/gateway-reviews/resolve` هر دو با **JWT کارمند دارای مجوز `TopUp.Review`** کار می‌کنند؛ `X-Service-Key` به‌تنهایی کافی نیست.
- نرخ تبدیل `rialPerNoor` را از `GET /api/settings/conversion-rate` (یا `gatewayReview.rialPerNoor` در فایل وضعیت) بخوانید و مبلغ ریالی (`amountNoor * rialPerNoor`) را با همان نرخ حساب کنید.

## کاربر

| نقش | شماره / منبع | رمز | کاربرد |
|---|---|---|---|
| کاربر مالک شارژ | `gatewayReview.buyer.phone` (پیش‌فرض `09120000002`) | `Manual-Test-2026` | ساخت شارژ تازه و خواندن موجودی/گردش کیف پول (`GET /api/wallet/balance`, `GET /api/wallet/transactions`) |
| کاربر بدون مجوز مالی | `09120000001` (`users[key=funded]`) | `Manual-Test-2026` | آزمون خطای `403` در مسیرهای ادمین و `resolve` |
| ادمین | `09120000003` (`users[key=admin]`) | `Manual-Test-2026` | استعلام جزئیات پرونده و بستن پرونده (`TopUp.Review`) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `gatewayReview.buyer`: `phone`، `externalUserId`، `internalUserId`، `balanceNoor` (پیش از شروع سناریو = ۱۱ نور بابت `G_PAID`).
- `gatewayReview.rialPerNoor`: نرخ لحظهٔ سید.
- `gatewayReview.items[]`: `clientReferenceCode`، `topUpRequestId`، `amountNoor`، `amountRial`، `caseId`، `reasonCode`، `bankTransactionId`، `bankReferenceNumber` برای هر ۵ مورد.

## قدم‌ها

### الف — ساخت شارژ معلق درگاه و استعلام پیش از باز شدن پرونده

> پیش از شروع، موجودی کاربر را با `GET /api/wallet/balance` به‌عنوان `b0` یادداشت کنید (در محیط تمیز `11` نور).

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن کاربر (`09120000002`) | `POST /api/topup/requests` با `{"amount":4,"paymentMethod":1}` | `200` با `topUpRequestId`، `clientReferenceId` (`TR-...`) و `amountRial = 4 * rialPerNoor`؛ موجودی کیف پول همچنان `b0` است |
| ۲ | Swagger با `X-Service-Key` | `GET /api/integrations/topups/by-reference/{G_PEND.clientReferenceCode}/confirmation` | `200` با `status="Pending"`، `paymentMethod="Gateway"`، `amountRial=G_PEND.amountRial`، `hasManualRefund=false` |
| ۳ | Swagger با `X-Service-Key` | `GET /api/integrations/topups/v1/gateway-reviews/{G_PEND.clientReferenceCode}` | `404` (هنوز پروندهٔ رسیدگی باز نشده است) |
| ۴ | Swagger با توکن ادمین | `GET /api/admin/topups/gateway-reviews/{G_PEND.clientReferenceCode}` | `200` با مشخصات کاربر (`userDisplayName`, `userPhone`)، `status="Pending"` و `review: null` |

### ب — باز کردن پروندهٔ رسیدگی (`open`) و تکرار آن

> یک شناسهٔ پرونده به‌نام `C_PEND` (مثلاً `"case-manual-pend-01"`) برای `G_PEND` انتخاب کنید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با `X-Service-Key` | `POST /api/integrations/topups/v1/gateway-reviews/open` با `{"clientReferenceCode": G_PEND, "caseId": C_PEND, "reasonCode": "NO_CALLBACK"}` | `200`؛ `topUpStatus="UnderReview"`، `reasonCode="NO_CALLBACK"`، `openedAtUtc` پر و `resolvedAtUtc=null` |
| ۲ | همان | **تکرار دقیق** گام ۱ با همان `clientReferenceCode`، `caseId` و `reasonCode` | `200` با همان خروجی (Idempotent) |
| ۳ | همان | `POST /api/integrations/topups/v1/gateway-reviews/open` روی `G_PEND` با همان `C_PEND` ولی `reasonCode: "VERIFY_UNKNOWN"` (یا با `caseId` متفاوت) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` |
| ۴ | همان | باز کردن پرونده روی `G_PAID` با `caseId: C_PEND` (متعلق به `G_PEND`) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` |
| ۵ | همان | `GET /api/integrations/topups/v1/gateway-reviews/{G_PEND}` | `200` با `caseId: C_PEND`، `revision: 1` و `topUpStatus: "UnderReview"` |

### ج — بستن با `NO_MATCHING_DEPOSIT`، بازگشایی (`reopen`) و کنترل نسخه

> دو UUID تازه به‌نام `OP1` و `OP2` بسازید؛ `TODAY` تاریخ امروز به قالب `YYYY-MM-DD` است.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `POST /api/admin/topups/gateway-reviews/resolve` با `clientReferenceCode: G_PEND`، `caseId: C_PEND`، `outcomeCode: "NO_MATCHING_DEPOSIT"`، `resolutionFinancialReferenceId: null` و `evidence: {operationId: OP1, expectedRevision: 1, checkedReportDate: TODAY, matchingDepositFound: false, note: "واریز یافت نشد"}` | `200`؛ `topUpStatus="Unresolved"`، `outcomeCode="NO_MATCHING_DEPOSIT"`، `resolvedAtUtc` پر و یک سطر `resolved` در `audit` |
| ۲ | Swagger با توکن کاربر | `GET /api/wallet/balance` و `GET /api/wallet/transactions` | موجودی همچنان `b0` و هیچ ردیف Ledger جدیدی ساخته نشده است |
| ۳ | Swagger با `X-Service-Key` | ثبت نتیجهٔ `Verified` روی `G_PEND` در وضعیت `Unresolved` (`POST /api/integrations/topups/gateway-results`) | `409` با `{code:"TOPUP_REQUIRES_REVIEW"}` (تا پیش از بازگشایی، نتیجهٔ بانکی جدید شارژ را تأیید نمی‌کند) |
| ۴ | Swagger با `X-Service-Key` | `POST /api/integrations/topups/v1/gateway-reviews/reopen` با `{"clientReferenceCode": G_PEND, "caseId": C_PEND, "evidenceId": "ev-reopen-01", "note": "درخواست پیگیری مجدد"}` | `200`؛ `topUpStatus="UnderReview"`، `revision: 2`، `resolvedAtUtc: null`، `outcomeCode: null` و حفظ سطر قبلی `resolved` همراه سطر جدید `evidence` در `audit` |
| ۵ | همان | تکرار دقیق گام ۴ با همان `evidenceId: "ev-reopen-01"` | `200`؛ `revision` همچنان `2` می‌ماند (Idempotent) |
| ۶ | Swagger با توکن ادمین | بستن با فرم قدیمی (`expectedRevision: 1` و `operationId: OP2`) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` |

### د — بستن با `MANUAL_REFUND`، تکرار و حفاظت دائمی پس از بازپرداخت دستی

> سه UUID تازه به‌نام `OP3`، `OP4` و `OP5` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `POST /api/integrations/topups/v1/gateway-reviews/resolve` (مسیر دوم `resolve`) با `clientReferenceCode: G_PEND`، `caseId: C_PEND`، `outcomeCode: "MANUAL_REFUND"`، `resolutionFinancialReferenceId: null` و `evidence: {operationId: OP3, expectedRevision: 2, checkedReportDate: TODAY, matchingDepositFound: false, manualRefundReference: "REFUND-MANUAL-01", note: "بازپرداخت دستی"}` | `200`؛ `topUpStatus="ManuallyRefunded"`، `manualRefundReference="REFUND-MANUAL-01"`، موجودی همچنان `b0` |
| ۲ | همان | **تکرار دقیق** گام ۱ با همان `OP3`، همان محتوا و همان کاربر ادمین | `200` با همان خروجی |
| ۳ | همان | تکرار با همان `OP3` ولی `note` متفاوت (مثلاً `"تغییر یادداشت"`) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` |
| ۴ | Swagger با `X-Service-Key` | بازگشایی (`reopen`) با `evidenceId: "ev-reopen-02"` | `200`؛ `revision: 3`، ولی وضعیت همچنان `ManuallyRefunded` و `manualRefundReference` حفظ می‌شود |
| ۵ | Swagger با `X-Service-Key` | `POST /api/integrations/topups/gateway-results` با `kind: "Verified"` و `eventId: "evt-late-ver-01"` روی `G_PEND` | `409` با `{code:"REVIEW_MANUAL_REFUND_NOT_SUPPORTED"}`؛ هیچ نوری ساخته نمی‌شود |
| ۶ | Swagger با توکن ادمین | تلاش برای بستن دوبارهٔ پروندهٔ بازگشایی‌شدهٔ `G_PEND` با `outcomeCode: "NO_MATCHING_DEPOSIT"` (`operationId: OP4`, `expectedRevision: 3`) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` (پس از `MANUAL_REFUND` به `Unresolved` برنمی‌گردد) |

### هـ — بستن با `PAID_AND_CONFIRMED` روی شارژ تأییدشده (`G_PAID`)

> یک UUID تازه به‌نام `OP6` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `POST /api/admin/topups/gateway-reviews/resolve` روی `G_PAID` با `outcomeCode: "MANUAL_REFUND"` و `manualRefundReference: "R-1"` | `422` با `{code:"REVIEW_MANUAL_REFUND_NOT_SUPPORTED"}` |
| ۲ | همان | `POST /api/admin/topups/gateway-reviews/resolve` روی `G_PAID` با `caseId: G_PAID.caseId`، `outcomeCode: "PAID_AND_CONFIRMED"`، `resolutionFinancialReferenceId: G_PAID.bankTransactionId` و `evidence: {operationId: OP6, expectedRevision: 1, checkedReportDate: TODAY, matchingDepositFound: true, depositReference: G_PAID.bankReferenceNumber, depositDate: TODAY, depositAmountRial: G_PAID.amountRial, note: "تأیید واریز"}` | `200`؛ `outcomeCode="PAID_AND_CONFIRMED"`، `topUpStatus="Confirmed"` |
| ۳ | Swagger با توکن کاربر | `GET /api/wallet/balance` و `GET /api/wallet/transactions` | موجودی همچنان `b0` است (بستن پرونده با `PAID_AND_CONFIRMED` اعتبار تازه‌ای نمی‌سازد) |

### و — بستن با `REVERSED_REJECTED` روی شارژ برگشت‌خورده (`G_REV`)

> دو UUID تازه به‌نام `OP7` و `OP8` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | تلاش برای `CONFIRMED_EXCEPTIONAL` روی `G_REV` (`operationId: OP7`, `expectedRevision: 1`, `matchingDepositFound: true`, `depositAmountRial: G_REV.amountRial`) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` (به‌علت نبود سابقهٔ `Verified` و وجود `ReverseSucceeded`) |
| ۲ | همان | `POST /api/admin/topups/gateway-reviews/resolve` روی `G_REV` با `caseId: G_REV.caseId`، `outcomeCode: "REVERSED_REJECTED"`، `resolutionFinancialReferenceId: G_REV.bankTransactionId` و `evidence: {operationId: OP8, expectedRevision: 1, checkedReportDate: TODAY, matchingDepositFound: false, note: "برگشت‌خورده در بانک"}` | `200`؛ `outcomeCode="REVERSED_REJECTED"`، `topUpStatus="Rejected"`، موجودی همچنان `b0` |

### ز — تعارض بانکی (`BANK_CONFLICT`) و تأیید استثنایی (`CONFIRMED_EXCEPTIONAL`) روی `G_EXC`

> دو UUID تازه به‌نام `OP9` و `OP10` بسازید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `GET /api/admin/topups/gateway-reviews/{G_EXC}` | `200`؛ `status="Rejected"`، `gatewayResultKind="Verified"`، `review.reasonCode="BANK_CONFLICT"` و در `review.audit` رویداد `Verified` ثبت است (به‌صورت خودکار تأیید نشده) |
| ۲ | همان | `POST /api/admin/topups/gateway-reviews/resolve` روی `G_EXC` با `outcomeCode: "CONFIRMED_EXCEPTIONAL"` و `depositAmountRial: G_EXC.amountRial - 1` (`operationId: OP9`, `expectedRevision: 1`) | `422` با `{code:"TOPUP_AMOUNT_MISMATCH"}` |
| ۳ | همان | همان درخواست با `depositAmountRial: G_EXC.amountRial`، `matchingDepositFound: true`، `resolutionFinancialReferenceId: G_EXC.bankTransactionId` و `operationId: OP10` | `200`؛ `topUpStatus="Confirmed"`، `outcomeCode="CONFIRMED_EXCEPTIONAL"` |
| ۴ | همان | **تکرار دقیق** گام ۳ با همان `OP10` | `200` با همان خروجی |
| ۵ | Swagger با توکن کاربر | `GET /api/wallet/balance` و `GET /api/wallet/transactions` | موجودی `b0 + 13` شده و دقیقاً **یک** ردیف `TopUp` به مبلغ `13` نور بابت `G_EXC` ثبت شده است (نه دو بار در تکرار) |

### ح — خطاهای احراز هویت، مجوز و ورودی نامعتبر

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بدون هدر `X-Service-Key` یا با کلید کوتاه/نامعتبر | `POST /api/integrations/topups/v1/gateway-reviews/open` | `401` با `{code:"SERVICE_UNAUTHORIZED"}` |
| ۲ | Swagger فقط با `X-Service-Key` (بدون JWT) | `POST /api/integrations/topups/v1/gateway-reviews/resolve` | `401` |
| ۳ | Swagger با JWT کاربر عادی (`09120000001`) | `GET /api/admin/topups/gateway-reviews/{G_PAID}` و `POST /api/admin/topups/gateway-reviews/resolve` | هر دو `403` |
| ۴ | Swagger با `X-Service-Key` | `POST /api/integrations/topups/v1/gateway-reviews/open` روی `clientReferenceCode: "TR-NOTEXIST"` | `404` با `{code:"TOPUP_NOT_FOUND"}` |
| ۵ | همان | `POST /api/integrations/topups/v1/gateway-reviews/open` روی `G_C2C` (کارت‌به‌کارت) | `409` با `{code:"REVIEW_CASE_CONFLICT"}` |
| ۶ | همان | `POST /api/integrations/topups/v1/gateway-reviews/open` با `reasonCode: "INVALID_REASON"` | `400` با `REVIEW_REASON_INVALID` |
| ۷ | Swagger با توکن ادمین | `POST /api/admin/topups/gateway-reviews/resolve` با `outcomeCode: "INVALID_OUTCOME"` | `400` با `REVIEW_OUTCOME_INVALID` |
| ۸ | Swagger با `X-Service-Key` | `POST /api/integrations/topups/gateway-results` روی شارژ تازه با همان `bankTransactionId` متعلق به `G_PAID` (`G_PAID.bankTransactionId`) | `409` با `{code:"REFNUM_CONFLICT"}` |

## نتیجهٔ موردانتظار

- تنها زمانی نور به کیف پول اضافه می‌شود که یا درگاه نتیجهٔ `Verified` روی شارژ `Pending`/`UnderReview` بفرستد، یا پروندهٔ `BANK_CONFLICT` با سابقهٔ معتبر `Verified` و مبلغ برابر از مسیر `CONFIRMED_EXCEPTIONAL` بسته شود.
- بستن با `NO_MATCHING_DEPOSIT`، `MANUAL_REFUND`، `PAID_AND_CONFIRMED` و `REVERSED_REJECTED` هیچ تغییر تازه‌ای در موجودی کیف پول و Ledger ایجاد نمی‌کند.
- پس از `MANUAL_REFUND`، ارسال دیرهنگام `Verified` نیز رد می‌شود و حفاظت دائمی برقرار می‌ماند.
- تکرار درخواست‌ها (`open`، `reopen`، `resolve` با همان `operationId`) یک‌باره است و تغییر محتوا/یادداشت/نسخهٔ قدیمی با `409` رد می‌شود.

## ثبت نتیجه

- موفق: بخش «رسیدگی مالی درگاه» در [گزارش شواهد](../Verification-Summary.md) — تاریخ، بیلد، `clientReferenceCode`ها و `caseId`ها.
- ناموفق: همان بخش — کد پاسخ، بدنهٔ پاسخ، و آخرین خط `logs/log-*.txt` بای‌مانی.

## خارج از دامنه / نیازمند تأیید

- اتصال واقعی به درگاه سپ (SEP) و پنل `report.sep.ir`: این سناریو فقط قرارداد بای‌مانی را می‌آزماید و هیچ فراخوانی واقعی به شاپرک/سپ انجام نمی‌دهد.
