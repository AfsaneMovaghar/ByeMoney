# ۰۶ — تسویهٔ سفارش‌های بای‌مانی و خروج نور

> دامنه: پیش‌نمایش و بستن دورهٔ تسویه (تگ گزارشی، بدون Ledger)، آرشیو دوره و تغییر فهرست بین پیش‌نمایش و بستن؛ خروج نور توسط ادمین (صلاحدید ادمین و استرداد پرداخت تکراری)، ثبت واریز، «پرداخت نشد»، تکرار درخواست و خطاها. قالب: [README](README.md).
> معیارها از I11-S و I11-W در [قرارداد اتصال](../../integration/ByeMoney-Strapi-Integration.md)، D09 و D10 در [تصمیم‌ها](../../architecture/ByeMoney-Decisions.md)، [ماتریس دسترسی](../../architecture/Permissions-Matrix.md) و ردیف‌های D3، D4، D5، E3 و A3 در [TASKS](../../../TASKS.md).

## پیش‌نیاز

- چهار سرویس بالا باشند (استرپی `:1337`، بای‌مانی `:5000`، فرانت `:3000`، PostgreSQL).
- `.\seed.ps1 -Action up` **با ادمین استرپی** اجرا شده باشد (دادهٔ پایه و نقش `Admin` بای‌مانی با هر ۸ مجوز).
- `.\seed.ps1 -Action buyers` **یک بار پیش از این سناریو** اجرا شود؛ نقش‌های `settlementBuyer` و `withdrawalBuyer` در همین دسته ساخته می‌شوند.
- `.\seed.ps1 -Action finance` اجرا شود. این مرحله روی `withdrawalBuyer` یک شارژ تأییدشدهٔ **دارای نرخ** (۲۰ نور، کلید تکرار ثابت) و یک شارژ **بدون نرخ** (۵ نور، مسیر عادی کارت‌به‌کارت) می‌سازد و شناسهٔ هر دو را در `finance.items` ثبت می‌کند.
- برای هر اجرای دوبارهٔ بخش تسویه، دستهٔ تازه بسازید (`-Action buyers` و سپس `-Action finance`)؛ خرید دوره برگشت‌ناپذیر است و سفارش‌های دورهٔ بسته‌شده دیگر در پیش‌نمایش نمی‌آیند.

**نمادها** (شناسه‌ها را از `ByeMoney/.artifacts/manual-test/seed-state.json` بردارید، حدس نزنید):

| نماد | چیست | کلید | مقدار ثابت |
|---|---|---|---|
| C1 | دورهٔ پایه، ۲۰ نور | `fixtures.basicCourseDocumentId` | — |
| CH1 / CH2 | فصل ۱ و ۲ دورهٔ فصلی، هرکدام ۱۰ نور | `fixtures.seasonalChapter1IntegrationId` / `seasonalChapter2IntegrationId` | `a0000001-…c001` / `a0000002-…c002` |
| SB | خریدار تسویه (هدف ۱۰۰ نور) | `buyers.items[key=settlementBuyer]` | — |
| WB | خریدار خروج (هدف ۱۰۰ نور + شارژهای finance) | `buyers.items[key=withdrawalBuyer]` | — |
| T1 | شارژ دارای نرخ همان WB | `finance.items[key=withdrawalBuyer].ratedTopUpId` | ۲۰ نور / ۲۰٬۰۰۰ ریال با نرخ ۱٬۰۰۰ |
| T2 | شارژ بدون نرخ همان WB | `finance.items[key=withdrawalBuyer].unratedTopUpId` | ۵ نور |

**قرارداد اجرا:**

- توکن هر کاربر از `POST http://localhost:1337/api/auth/password/login` گرفته و در Swagger با دکمهٔ **Authorize** (`Bearer <توکن>`) گذاشته می‌شود.
- نرخ تبدیل را از `GET /api/settings/conversion-rate` بخوانید؛ همهٔ مبالغ این سند با نرخ `۱ نور = ۱٬۰۰۰ ریال` نوشته شده‌اند. اگر نرخ عوض شد، ریال هر گام را از نرخ همان اجرا حساب کنید.
- پیش از بخش‌های دال تا ز، موجودی `WB` را با `GET /api/wallet/balance` بخوانید و `balance` و `giftBalance` را به‌عنوان `b0` یادداشت کنید.
- مبالغ نور تا ۴ رقم اعشار گرد می‌شوند؛ مقایسه‌ها با همین دقت انجام شود.

## کاربر

| نقش | شماره / منبع | رمز | کاربرد |
|---|---|---|---|
| ادمین | `09120000003` (`users[key=admin]`) | `Manual-Test-2026` | همهٔ بخش‌ها (۸ مجوز، شامل `Purchases.Settle` و `Noor.Withdraw`) |
| خریدار تسویه | `buyers.items[key=settlementBuyer]` | `Manual-Test-2026` | بخش‌های ب و ج (ساخت سفارش پرداخت‌شده) |
| خریدار خروج | `buyers.items[key=withdrawalBuyer]` | `Manual-Test-2026` | بخش‌های ج (سفارش سوم)، د، ه و و |
| کاربر دارای هدیه | `09120000001` (`users[key=funded]`) | `Manual-Test-2026` | بخش ه-۲ (نور هدیه از مسیر خروج شمرده نشود) و بخش ح (کاربر بدون مجوز) |

## شناسهٔ دادهٔ ساخته‌شده

از `ByeMoney/.artifacts/manual-test/seed-state.json`:

- `buyers.items[]` برای `settlementBuyer` و `withdrawalBuyer` (`externalUserId`، `internalUserId`، `phone`، `targetBalanceNoor`).
- `finance.items[key=withdrawalBuyer]`: `ratedTopUpId` (`T1`)، `ratedTopUpAmountNoor`، `ratedTopUpAmountRial`، `unratedTopUpId` (`T2`)، `rialPerNoor` و `balanceNoor` لحظهٔ سید.
- `fixtures.basicCourseDocumentId` (`C1`)، `fixtures.seasonalChapter1IntegrationId` (`CH1`)، `fixtures.seasonalChapter2IntegrationId` (`CH2`).

## قدم‌ها

### الف — پیش‌نمایش دورهٔ تسویه: شکل پاسخ، جمع‌ها و صفحه‌بندی

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `GET /api/admin/order-settlements/preview?page=1&pageSize=20` | `200`؛ `{orderCount, totalRegularNetAmountInNoor, totalGiftNetAmountInNoor, totalNetAmountInNoor, previewToken, orders:{items,page,pageSize,totalCount}}` |
| ۲ | همان | بررسی جمع‌ها | `totalNetAmountInNoor = totalRegularNetAmountInNoor + totalGiftNetAmountInNoor`، `orderCount = orders.totalCount` و جمع هر ردیف با `regularNetAmountInNoor + giftNetAmountInNoor = netAmountInNoor` |
| ۳ | همان | بررسی `previewToken` | رشتهٔ ۶۴ نویسهٔ hex؛ فرانت آن را تفسیر نمی‌کند |
| ۴ | همان | تکرار همان درخواست بدون هیچ تغییری در داده | پاسخ یکسان، از جمله همان `previewToken`؛ پیش‌نمایش هیچ رکورد، رزرو یا Ledgerی نمی‌سازد |
| ۵ | همان | `pageSize=101`، `pageSize=0`، `page=0` | هر سه `400` با خطای اعتبارسنجی پروژه (نه `{code,error}`) |

### ب — سفارش قابل تسویه، تغییر فهرست بین پیش‌نمایش و بستن، بستن و آرشیو

> دو UUID تازه به‌نام `S1` و `S2` بسازید و در تکرار هر درخواست همان‌ها را بفرستید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بای‌مانی با توکن ادمین | `POST /api/admin/carts/orders` با `{cartId:S1, beneficiaryExternalUserId:SB, items:[{C1, course, 1}, {CH2, course_chapter, 1}], isFree:false, freeReason:null}` | `200` با `status="Paid"`؛ سفارش `cart:S1` با مبلغ ۳۰ نور |
| ۲ | Swagger با توکن ادمین | `GET /api/admin/order-settlements/preview` | ردیف `cart:S1` با `netAmountInNoor=30`؛ `previewToken` را به‌عنوان `P1` یادداشت کنید. سفارش پرداخت‌نشدهٔ `AwaitingPayment` (سبد `Ready`) در پیش‌نمایش **نیست** |
| ۳ | Swagger با توکن ادمین | سفارش دوم: `POST /api/admin/carts/orders` با `{cartId:S2, beneficiaryExternalUserId:SB, items:[{CH1, course_chapter, 1}], isFree:false}` | `200` و `status="Paid"`؛ مبلغ ۱۰ نور |
| ۴ | Swagger با توکن ادمین | `POST /api/admin/order-settlements` با `{title:"تسویهٔ آزمون دستی", note:"…", previewToken:P1}` | `409` با `{code:"SETTLEMENT_PREVIEW_CHANGED", error:"…"}`؛ هیچ دوره‌ای ساخته نمی‌شود و فرانت باید پیش‌نمایش را دوباره بگیرد |
| ۵ | Swagger با توکن ادمین | پیش‌نمایش تازه | هر دو سفارش در فهرست‌اند؛ `previewToken` تازه را `P2` بگذارید |
| ۶ | Swagger با توکن ادمین | `POST /api/admin/order-settlements` با `{title:"تسویهٔ آزمون دستی", note:"یادداشت آزمون", previewToken:P2}` | `201` با DTO دوره: `periodId`, `title`, `note`, `orderCount` برابر تعداد ردیف‌های پیش‌نمایش (در محیط تمیز ۲)، سه جمع، `closedByExternalUserId` برابر `externalUserId` ادمین، `closedAtUtc` و `scopeId="TARHE_ELAHI"` |
| ۷ | Swagger با توکن ادمین | تکرار همان درخواست با همان `P2` | `409` با `{code:"SETTLEMENT_EMPTY"}` (اگر در این فاصله سفارش قابل‌تسویهٔ تازه ساخته نشده باشد)؛ دورهٔ دوم ساخته نمی‌شود |
| ۸ | Swagger با توکن ادمین | `GET /api/admin/order-settlements?page=1&pageSize=20` | `200`؛ دورهٔ تازه اول فهرست؛ دورهٔ بسته‌شده ویرایش یا حذف نمی‌شود (فقط `GET`) |
| ۹ | Swagger با توکن ادمین | `GET /api/admin/order-settlements/{periodId}/orders?page=1&pageSize=20` | `200`؛ همان سفارش‌های ثبت‌شده در دوره (در محیط تمیز دو سفارش) با Snapshot لحظهٔ بستن (`orderId`, `sourceType`, `buyerExternalUserId`, `orderCreatedAtUtc`, سه مبلغ) |
| ۱۰ | همان | `GET /api/admin/order-settlements/{periodId}/orders` با `periodId` تصادفی | `404` |
| ۱۱ | همان | `GET /api/admin/order-settlements/{periodId}/orders?pageSize=0` | `400` خطای اعتبارسنجی |
| ۱۲ | Swagger با توکن `SB` و توکن ادمین | `GET /api/wallet/balance` و `GET /api/wallet/transactions` پیش و پس از بستن دوره | موجودی و شمار ردیف‌های Ledger **بدون تغییر**؛ جمع Ledger و مانده یکی می‌ماند (تسویه فقط تگ است) |

### ج — بستن هم‌زمان و رفتار رقابتی

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بای‌مانی با توکن ادمین | سفارش سوم: `POST /api/admin/carts/orders` با `{cartId:S3, beneficiaryExternalUserId:WB, items:[{C1, course, 1}], isFree:false}` | `200` و `status="Paid"`؛ مبلغ ۲۰ نور |
| ۲ | Swagger با توکن ادمین | پیش‌نمایش تازه | `cart:S3` (و هر سفارش قابل‌تسویهٔ تازهٔ دیگر) در فهرست است و سفارش‌های دورهٔ قبل دیگر نمی‌آیند؛ `previewToken` را `P3` بگذارید |
| ۳ | PowerShell | دو `POST /api/admin/order-settlements` هم‌زمان با همان `P3` (نمونهٔ زیر) | دقیقاً یکی `201` و دیگری `409`؛ کد دومی `SETTLEMENT_ALREADY_SETTLED` است اگر هر دو درخواست فهرست را پیش از ثبت اولی خوانده باشند، و `SETTLEMENT_EMPTY` اگر دومی بعد از ثبت اولی فهرست را خوانده باشد. در هیچ حالتی دو دوره با همان سفارش ساخته نمی‌شود |

```powershell
$body = @{ title = 'تسویهٔ هم‌زمان'; note = ''; previewToken = $P3 } | ConvertTo-Json
$jobs = 1..2 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($uri, $token, $body)
        try {
            $r = Invoke-WebRequest -Uri $uri -Method Post -UseBasicParsing `
                -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body $body
            [int]$r.StatusCode
        } catch { [int]$_.Exception.Response.StatusCode }
    } -ArgumentList 'http://localhost:5000/api/admin/order-settlements', $adminJwt, $body
}
$jobs | Wait-Job | Receive-Job
```

### د — خروج نور: صلاحدید ادمین، واریز، «پرداخت نشد» و تکرار درخواست

> `b0` موجودی `WB` پیش از این بخش است (سید: ۱۲۵ نور = ۱۰۰ شارژ + ۲۰ دارای نرخ + ۵ بدون نرخ؛ با نرخ ۱٬۰۰۰ ریال). UUID به‌نام `W1` بسازید و در تکرارها همان را بفرستید.

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین (multipart) | `POST /api/admin/noor-withdrawals` با `operationId:W1`، `userExternalId:WB`، `kind:AdminDiscretionary`، `amountRial:20000`، `beneficiaryName`، `beneficiaryCardNumber` (۱۶ رقم)، `description` (بدون `paymentReference` و فیش) | `200` با DTO: `status="PendingPayment"`، `amountNoor=20`، `amountRial=20000`، `createdByExternalUserId` ادمین، `hasReceipt=false`، `paidAtUtc=null` |
| ۲ | Swagger با توکن `WB` | `GET /api/wallet/balance` و `GET /api/wallet/transactions` | موجودی `b0 − 20` و یک ردیف `Withdrawal` با `amountNoor=-20` و مرجع همان خروج؛ `giftBalance` تغییر نمی‌کند |
| ۳ | Swagger با توکن ادمین | **تکرار دقیق** گام ۱ با همان `W1` و همان محتوا | `200` با همان `id`؛ موجودی دیگر کم نمی‌شود و ردیف Ledger دوم ساخته نمی‌شود |
| ۴ | همان | همان `W1` با `amountRial:10000` | `409` با `{code:"NOOR_WITHDRAWAL_IDEMPOTENCY_CONFLICT"}` |
| ۵ | همان | `GET /api/admin/noor-withdrawals/{id}` و `GET /api/admin/noor-withdrawals/{id}/receipt` | DTO همان خروج؛ فیش `404` (بدون فیش) |
| ۶ | همان | `POST /api/admin/noor-withdrawals/{id}/payment` با `paymentReference` و فایل فیش png | `200` با `status="Paid"`، `paidAtUtc` و `paidByExternalUserId` پر، `hasReceipt=true`؛ موجودی تغییر نمی‌کند (نور در گام ۱ کم شده) |
| ۷ | همان | تکرار گام ۶ با همان مرجع و همان فایل؛ سپس همان درخواست با مرجع دیگر | تکرار: `200` با همان نتیجه و `paidAtUtc` بدون تغییر. مرجع متفاوت: `409` با `{code:"NOOR_WITHDRAWAL_NOT_PENDING"}` |
| ۸ | همان | `GET /api/admin/noor-withdrawals/{id}/receipt` | `200` با فایل تصویر همان فیش |
| ۹ | همان | `POST /api/admin/noor-withdrawals/{id}/reverse` با `{reason:"برگشت آزمون"}` روی خروج **Paid** | `409` با `{code:"NOOR_WITHDRAWAL_NOT_PENDING"}` (طبق D09 برگشت فقط برای «در انتظار پرداخت») |
| ۱۰ | همان | خروج دوم با `operationId` تازه، `amountRial:20000` | `200` `PendingPayment`؛ موجودی `b0 − 40` |
| ۱۱ | همان | `POST /api/admin/noor-withdrawals/{id2}/reverse` با `{reason:"واریز انجام نشد"}` | `200` با `status="Reversed"`، `reversalReason`، `reversedAtUtc` و `reversedByExternalUserId`؛ موجودی `b0 − 20` و یک ردیف `WithdrawalReversal` با `amountNoor=+20` |
| ۱۲ | همان | تکرار گام ۱۱ | `200` با همان نتیجه؛ برگشت دوم و ردیف جبرانی دوم ساخته نمی‌شود |
| ۱۳ | همان | `GET /api/admin/noor-withdrawals?status=PendingPayment`، `?status=Paid`، `?status=Reversed`، `?userExternalId=WB` و `?userExternalId=SB` | هر فیلتر فقط ردیف‌های همان وضعیت/کاربر را می‌دهد، جدیدترین اول؛ `userExternalId=SB` فهرست خالی برمی‌گرداند |
| ۱۴ | همان | `GET /api/admin/noor-withdrawals?limit=201` و `?limit=0` و `?status=Unknown` | هر سه `400` خطای اعتبارسنجی |
| ۱۵ | همان | خروج سوم با `operationId` تازه، `amountRial:20000` **به‌همراه** `paymentReference` و فیش در همان درخواست (راه دوم D09) | `200` با `status="Paid"` بی‌مرحلهٔ «در انتظار»، `hasReceipt=true`، `paidAtUtc` و `paidByExternalUserId` پر؛ موجودی `b0 − 40` |
| ۱۶ | همان | تکرار دقیق گام ۱۵ با همان `operationId` و همان محتوا | `200` با همان `id`؛ موجودی تغییر نمی‌کند |

### ه — خروج نور: خطاها

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `amountRial = (موجودی نور عادی WB + 1) × نرخ` | `400` با `{code:"NOOR_WITHDRAWAL_INSUFFICIENT_BALANCE"}`؛ موجودی و Ledger بدون تغییر |
| ۲ | Swagger با توکن ادمین، کاربر `funded` | (الف) `amountRial = (balance − giftBalance + 1) × نرخ`؛ (ب) `amountRial = (balance − giftBalance) × نرخ` | (الف) `400 INSUFFICIENT_BALANCE` با اینکه مانده کل بیشتر است ← نور هدیه شمرده نمی‌شود؛ (ب) `200` و بعد از آزمون با `reverse` برگشت بزنید |
| ۳ | Swagger با توکن ادمین | `operationId=00000000-0000-0000-0000-000000000000`؛ `kind` نامعتبر؛ `amountRial=0`؛ `amountRial=1500.5`؛ نام صاحب حساب خالی؛ `description` خالی؛ نه کارت نه شبا؛ کارت ۱۵رقمی؛ شبا بدون `IR`؛ `paymentReference` بدون فیش؛ فیش بدون `paymentReference` | همه `400` با شکل عمومی خطای اعتبارسنجی پروژه؛ هیچ رکوردی ساخته نمی‌شود |
| ۴ | همان | `userExternalId="nonexistent-user-id-000"` | `404` با `{code:"NOOR_WITHDRAWAL_USER_NOT_FOUND"}` |
| ۵ | همان | `GET /api/admin/noor-withdrawals/{guid تصادفی}` و `POST …/{guid تصادفی}/reverse` | هر دو `404` با `{code:"NOOR_WITHDRAWAL_NOT_FOUND"}` |
| ۶ | همان | فایل فیش غیرتصویر (مثلاً `.txt`) در `POST /api/admin/noor-withdrawals` | `400` با `{code:"NOOR_WITHDRAWAL_INVALID_RECEIPT"}` |
| ۷ | — | `amountRial` یک‌ریالی با نرخ ۱٬۰۰۰ | `200` (۰٫۰۰۱ نور)؛ یعنی `NOOR_WITHDRAWAL_AMOUNT_TOO_SMALL` با نرخ فعلی و ریال صحیح **قابل وقوع نیست** (فقط با نرخ بالای ۲۰٬۰۰۰ ریال برای هر نور). این کد با دادهٔ فعلی آزموده نمی‌شود |

### و — استرداد پرداخت تکراری

> `T1` شارژ دارای نرخ و `T2` شارژ بدون نرخ همان `WB` است (از `finance.items`).

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger با توکن ادمین | `GET /api/admin/noor-withdrawals/refundable-topups?userExternalId=WB` | `200`؛ `T1` با `amountNoor=20`، `amountRial=20000`، `rialPerNoor=1000` و `hasActiveRefund=false`؛ `T2` **در فهرست نیست** (نرخ ثبت‌شده ندارد) |
| ۲ | Swagger با توکن ادمین | `POST /api/admin/noor-withdrawals` با `kind:DuplicatePaymentRefund`، `relatedTopUpId:T1`، `amountRial:20000`، `operationId` تازه | `200` `PendingPayment`؛ `amountNoor=20` و `relatedTopUpId=T1`؛ موجودی `b0 − 40` |
| ۳ | همان | تکرار گام ۱ | `hasActiveRefund=true` برای `T1` (رکورد «در انتظار» هم فعال شمرده می‌شود) |
| ۴ | همان | درخواست تازه با `operationId` دیگر و همان `T1` و همان مبلغ | `409` با `{code:"NOOR_WITHDRAWAL_TOPUP_ALREADY_REFUNDED"}` |
| ۵ | همان | `kind:DuplicatePaymentRefund` با `relatedTopUpId:T2` | `400` با `{code:"NOOR_WITHDRAWAL_TOPUP_INVALID"}` |
| ۶ | همان | `kind:DuplicatePaymentRefund` با `amountRial = 21000` (بیش از ریال همان شارژ) | `400` با `{code:"NOOR_WITHDRAWAL_AMOUNT_EXCEEDS_TOPUP"}` |
| ۷ | همان | (الف) `kind:DuplicatePaymentRefund` بدون `relatedTopUpId`؛ (ب) `kind:AdminDiscretionary` با `relatedTopUpId:T1` | هر دو `400` خطای اعتبارسنجی |
| ۸ | همان | `POST /api/admin/noor-withdrawals/{id گام ۲}/reverse` با `{reason:"مبلغ اشتباه"}` | `200` `Reversed`؛ موجودی `b0 − 20`؛ گام ۱ دوباره `hasActiveRefund=false` نشان می‌دهد |
| ۹ | همان | استرداد دوبارهٔ `T1` با `operationId` تازه و سپس `POST …/{id}/payment` با مرجع و فیش | `200` `Paid`؛ موجودی `b0 − 40`؛ فهرست قابل استرداد دیگر `hasActiveRefund=true` دارد |
| ۱۰ | همان | `POST …/{id گام ۹}/reverse` | `409` `NOOR_WITHDRAWAL_NOT_PENDING` |
| ۱۱ | Swagger با توکن `WB` | `GET /api/wallet/transactions` | ردیف‌های `Withdrawal` (منفی) و `WithdrawalReversal` (مثبت) با مرجع همان خروج‌ها؛ جمع همهٔ ردیف‌ها برابر `balance` فعلی |

### ز — رابط کاربری پنل ادمین

| # | صفحه | کار | انتظار |
|---|---|---|---|
| ۱ | `/admin/orders/settlements` | ورود با ادمین | کارت «دورهٔ جاری» با سه جمع (نور عادی، هدیه، جمع کل)، نکته‌های ثابت صفحه و فهرست سفارش‌های تسویه‌نشده؛ باز کردن جزئیات یک ردیف |
| ۲ | همان | «بستن دوره» ← پر کردن عنوان و یادداشت ← «تأیید و بستن دوره» | پیام موفقیت با تعداد سفارش؛ فهرست جاری تازه می‌شود و دوره در «آرشیو دوره‌ها» با سه جمع و نام بستن‌کننده می‌آید |
| ۳ | همان | وقتی سفارش تسویه‌نشده‌ای نیست | دکمهٔ «بستن دوره» غیرفعال با عنوان «سفارش تسویه‌نشده‌ای نیست» |
| ۴ | `/admin/users` | دکمهٔ «خروج نور» روی ردیف `WB` | فرم خروج: موجودی نور عادی قابل خروج، نرخ و معادل تخمینی نور؛ پس از ثبت، خلاصهٔ «نور کسرشده از کیف پول» و «مبلغ واریز» |
| ۵ | همان فرم | انتخاب نوع «استرداد پرداخت تکراری» | فهرست شارژهای تأییدشدهٔ همان کاربر؛ `T1` قابل انتخاب، `T2` در فهرست نیست؛ شارژ دارای استرداد فعال غیرقابل انتخاب با برچسب «استرداد فعال دارد» |
| ۶ | `/admin/noor-withdrawals` | ورود با ادمین | فیلتر وضعیت با پیش‌فرض «در انتظار پرداخت»؛ ردیف‌های در انتظار دکمهٔ «پرداخت شد» و «پرداخت نشد» دارند؛ ردیف پرداخت‌شده مرجع پرداخت و «مشاهدهٔ فیش» دارد؛ ردیف برگشت‌خورده علت را نشان می‌دهد و اقدامی ندارد |
| ۷ | همان | «پرداخت شد» بدون فیش یا بدون مرجع | خطای فرم و هیچ درخواستی ثبت نمی‌شود |

### ح — دسترسی

| # | صفحه یا Swagger | درخواست | انتظار |
|---|---|---|---|
| ۱ | Swagger بدون توکن | `GET /api/admin/order-settlements/preview`، `GET /api/admin/noor-withdrawals`، `POST /api/admin/noor-withdrawals` | همه `401` |
| ۲ | Swagger با توکن کاربر عادی (`09120000001`) | `GET /api/admin/order-settlements/preview`، `GET /api/admin/noor-withdrawals`، `POST /api/admin/noor-withdrawals`، `POST /api/admin/order-settlements` | همه `403` (نه `Purchases.View`، نه `Noor.Withdraw`، نه `Purchases.Settle`) |
| ۳ | مرورگر با نشست کاربر عادی | رفتن به `/admin/noor-withdrawals` و `/admin/orders/settlements` | گارد نقش در layout کاربر غیرادمین را به صفحهٔ اصلی هدایت می‌کند و هیچ فهرستی بارگذاری نمی‌شود. صفحهٔ ۴۰۳ درون پنل فقط برای ادمینی است که مجوز همان مسیر را ندارد (با نقش تک‌ادمینی فاز اول دیده نمی‌شود) |
| ۴ | Swagger با توکن ادمین | همان مسیرهای گام ۲ | `200` / `201` مطابق بخش‌های بالا |

## نتیجهٔ موردانتظار

- تسویه فقط برچسب گزارش است: بستن دوره هیچ ردیف Ledger، تغییر موجودی یا جابه‌جایی نور ندارد؛ هر سفارش حداکثر در یک دوره ثبت می‌شود و دورهٔ بسته‌شده ویرایش/حذف نمی‌شود.
- فهرست پیش‌نمایش و بستن با `previewToken` تطبیق می‌شوند؛ هر تغییر (سفارش تازه، لغو، برگشتی، تغییر سهم عادی/هدیه) `409 SETTLEMENT_PREVIEW_CHANGED` می‌دهد و ثبت نمی‌شود.
- خروج فقط از نور عادی است: نور هدیه نه شمرده می‌شود و نه خارج؛ مبلغ ریالی ورودی است و نور با نرخ فعلی (صلاحدید) یا نرخ ثبت‌شدهٔ شارژ (استرداد) حساب می‌شود.
- هر `operationId` یک اثر مالی دارد: تکرار دقیق بی‌اثر است و تغییر محتوا `409` می‌گیرد؛ ثبت واریز اثر Ledger ندارد و تکرارش همان نتیجه را برمی‌گرداند.
- «پرداخت نشد» فقط برای خروج در انتظار است، نور را با یک ثبت جبرانی برمی‌گرداند و خروج پرداخت‌شده را تغییر نمی‌دهد.

## ثبت نتیجه

- موفق: بخش «تسویه و خروج نور» در [گزارش شواهد](../Verification-Summary.md) — تاریخ، نسخهٔ بیلد، `periodId`، `orderId`های دوره، `withdrawalId`ها و `operationId`ها.
- ناموفق: همان بخش — کد پاسخ، بدنهٔ پاسخ، و آخرین خط `logs/log-*.txt` بای‌مانی یا لاگ کنسول فرانت.

## خارج از دامنه / نیازمند تأیید

- هیچ فراخوانی بانکی، پستکس یا پیامک انجام نمی‌شود؛ واریز ریالی واقعی انجام نمی‌شود و فیش/شناسهٔ پرداخت دستی وارد می‌شود.
- انتقال بانکی بین ادمین‌ها خارج از سیستم است (D10)؛ تسویه همین را ثبت می‌کند و پولی جابه‌جا نمی‌کند.
- استرداد پرداخت تکراری روی شارژ درگاه واقعی (SEP) اینجا آزموده نمی‌شود؛ فقط شارژ کارت‌به‌کارت دارای نرخ.
- ساخت مرسوله و تغییر وضعیت تحویل کالا در سناریوی ۰۲ و ۰۴ است؛ این سناریو فقط شرط «قابل تسویه» را با سفارش دوره‌ای می‌سنجد.

## اختلاف کد و سند

- ~~**(نیازمند تصمیم/تغییر کد)** فیش نامعتبر در `POST /api/admin/noor-withdrawals` و `…/{id}/payment` پاسخ `400` با بدنهٔ `{code:"NOOR_WITHDRAWAL_INVALID_RECEIPT", error}` می‌دهد...~~ *(در ۲۵ نوامبر ۲۰۲۶ هم‌تراز شد: استثنای `400 NOOR_WITHDRAWAL_INVALID_RECEIPT` در متن عمومی I11-W نیز ثبت شد و با جدول مسیرها و مصرف‌کنندهٔ فرانت یکسان است.)*
- ~~**(فقط سند)** I11-W دو کد خطای موجود در کد را فهرست نکرده است: `NOOR_WITHDRAWAL_RATE_UNAVAILABLE` (نرخ تبدیل نامعتبر) و `NOOR_WITHDRAWAL_INVALID_RECEIPT`.~~ *(انجام شد.)*
- **(فقط سند)** I11-S اثر انگشت پیش‌نمایش را «شناسه و مبلغ خالص» می‌گوید؛ کد علاوه بر شناسه، سهم نور عادی و هدیهٔ هر سفارش را هم در اثر انگشت می‌آورد، پس تغییر فقط تقسیم عادی/هدیه هم پیش‌نمایش را باطل می‌کند (سخت‌گیرانه‌تر از متن سند). همچنین `NOOR_WITHDRAWAL_AMOUNT_TOO_SMALL` با نرخ فعلی قابل وقوع نیست. *(در I11-S ثبت شد.)*

پیگیری این موارد: یادداشت ۲۰۲۶-۱۱-۲۵ در بخش «یادداشت‌های خارج از اسکوپ» [TASKS](../../../TASKS.md).
