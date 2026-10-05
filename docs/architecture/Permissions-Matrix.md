# ماتریس دسترسی و مجوزهای ByeMoney (Permission Matrix)

> [!IMPORTANT]
> این سند بر اساس بازتاب (Reflection) مستقیم کدهای واقعی پروژه، کنترلرها و `Permissions.cs` تولید شده است و منعکس‌کننده دقیق وضعیت واقعی اندپوینت‌ها و پالیسی‌ها است.

## ۱. فهرست مجوزهای تعریف‌شده در سامانه (Permissions)

| کد پرمیژن (Domain) | شناسه پالیسی در API (Policy) | توضیح و حوزه عملکرد |
| :--- | :--- | :--- |
| `Noor.Inject` | `RequireNoorInject` | مجوز تزریق یا صدور مستقیم نور |
| `TopUp.Review` | `RequireTopUpReview` | مجوز بررسی، تأیید یا رد درخواست‌های شارژ حساب |
| `Courses.Manage` | `RequireCoursesManage` | مجوز ثبت و مدیریت خرید دوره به‌نیابت از کاربر |
| `Wallet.View` | `RequireWalletView` | مجوز استعلام دسته‌جمعی مانده کیف‌پول‌ها |

## ۲. ماتریس احراز هویت و دسترسی اندپوینت‌ها (Endpoint Authorization Matrix)

سطح دسترسی جدول فقط وضعیت JWT و Policy را نشان می‌دهد. مسیرهای GatewayTopUp با وجود برچسب Anonymous، با کلید سرویس و فیلتر اختصاصی محافظت می‌شوند.

| ماژول / کنترلر | متد | مسیر (Route) | سطح دسترسی / پالیسی | پرمیژن مورد نیاز |
| :--- | :--- | :--- | :--- | :--- |
| AdminAssistedTopUp | `POST` | `/api/admin/topups/assisted` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminCourses | `POST` | `/api/admin/courses/purchase` | نیازمند پالیسی `RequireCoursesManage` | `Courses.Manage` |
| AdminGatewayReview | `GET` | `/api/admin/topups/gateway-reviews/{clientReferenceCode}` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminGatewayReview | `POST` | `/api/admin/topups/gateway-reviews/resolve` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminTopUp | `POST` | `/api/admin/topups/{id:guid}/confirm` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminTopUp | `GET` | `/api/admin/topups/by-reference/{clientReferenceCode}` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminTopUp | `GET` | `/api/admin/topups/permissions` | احراز هویت شده (JWT) | — |
| AdminTopUp | `POST` | `/api/admin/topups/{id:guid}/reject` | نیازمند پالیسی `RequireTopUpReview` | `TopUp.Review` |
| AdminWallet | `POST` | `/api/admin/wallets/batch-balances` | نیازمند پالیسی `RequireWalletView` | `Wallet.View` |
| Auth | `POST` | `/api/auth/sync` | احراز هویت شده (JWT) | — |
| Courses | `POST` | `/api/courses/purchase` | احراز هویت شده (JWT) | — |
| GatewayTopUp | `POST` | `/api/integrations/topups/gateway-cancellations` | عمومی (Anonymous) | — |
| GatewayTopUp | `POST` | `/api/integrations/topups/gateway-confirmations` | عمومی (Anonymous) | — |
| GatewayTopUp | `GET` | `/api/integrations/topups/by-reference/{clientReferenceCode}/confirmation` | عمومی (Anonymous) | — |
| GatewayTopUp | `GET` | `/api/integrations/topups/v1/gateway-reviews/{clientReferenceCode}` | عمومی (Anonymous) | — |
| GatewayTopUp | `POST` | `/api/integrations/topups/v1/gateway-reviews/open` | عمومی (Anonymous) | — |
| GatewayTopUp | `POST` | `/api/integrations/topups/gateway-results` | عمومی (Anonymous) | — |
| GatewayTopUp | `POST` | `/api/integrations/topups/v1/gateway-reviews/reopen` | عمومی (Anonymous) | — |
| Settings | `GET` | `/api/settings/conversion-rate` | عمومی (Public / بدون Authorize) | — |
| TopUp | `POST` | `/api/topup/requests` | احراز هویت شده (JWT) | — |
| TopUpReceipt | `GET` | `/api/topup/requests/{topUpId:guid}/receipt` | احراز هویت شده (JWT) | — |
| Users | `POST` | `/api/users` | عمومی (Public / بدون Authorize) | — |
| Wallet | `GET` | `/api/wallet/balance` | احراز هویت شده (JWT) | — |

## ۳. اصول معماری و قواعد RBAC در ByeMoney

1. **جداسازی Role از Endpoint:** هیچ کنترلر یا اکشنی نباید مستقیماً از `[Authorize(Roles = ...)]` استفاده کند. تمام کنترلرها باید منحصراً بر پایه Policy / Permission باشند.
2. **نقش‌ها به عنوان کانتینر پرمیژن:** نقش `Admin` صرفاً مجموعه‌ای از این پرمیژن‌ها در پایگاه داده است و از طریق `RbacClaimsTransformation` به Claimهای کاربر تبدیل می‌شود.
3. **تغییرناپذیری نام‌های دیتابیس بدون Migration:** کدهای پرمیژن موجود در دیتابیس (`Noor.Inject` و `TopUp.Review`) همواره با داده‌های Seed و جداول `Permissions` و `RolePermissions` هماهنگ هستند.
