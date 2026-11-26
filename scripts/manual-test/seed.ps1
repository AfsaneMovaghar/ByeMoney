<#
.SYNOPSIS
    بستهٔ دادهٔ تست دستی بای‌مانی — ساخت، بررسی و بازسازی.

.DESCRIPTION
    دادهٔ کاربران و همهٔ دادهٔ مالی فقط از مسیر API ساخته می‌شود
    (ثبت‌نام استرپی، همگام‌سازی بای‌مانی، درخواست شارژ، تأیید ادمین، هدیه).
    تنها نوشتن مستقیم در دیتابیس، تخصیص نقش Admin در جدول‌های RBAC و پرچم
    «مسدود» کاربر استرپی است؛ هیچ سطر دفتر کل یا کیف پول نوشته یا ویرایش نمی‌شود.

    اجرا فقط روی میزبان‌های loopback مجاز است تا امکان اجرای تصادفی روی
    محیط دیگر وجود نداشته باشد.

.PARAMETER Action
    up     — ساخت یا تکمیل دادهٔ تست (تکرارپذیر، بدون دادهٔ تکراری)
    verify — فقط بررسی داده از مسیر API، بدون هیچ تغییری
    reset  — حذف دیتابیس توسعهٔ بای‌مانی و ساخت دوبارهٔ خالی (نیازمند راه‌اندازی مجدد API)
    buyers         — ساخت یک دستهٔ تازهٔ خریداران (بدون هیچ خرید و مالکیت قبلی) برای سناریوی
                     «سبد و خرید»؛ هر اجرا دستهٔ تازه می‌سازد و دستهٔ قبلی را در فایل وضعیت جایگزین می‌کند
    shipping-flags — ثبت غیرمالی پرچم «برگشتی» یا «هشدار پستی» روی یک قلم ارسال‌شده (Shipped) در
                     دیتابیس توسعه برای تست دستی بدون فراخوانی پستکس واقعی
    finance        — ساخت دادهٔ سناریوی «تسویه و خروج نور»: یک شارژ تأییدشدهٔ دارای نرخ (برای استرداد
                     پرداخت تکراری) و یک شارژ کارت‌به‌کارت بدون نرخ روی خریدار خروج
    gateway-review — ساخت دادهٔ سناریوی «رسیدگی مالی درگاه»: شارژ معلق درگاه، پروندهٔ باز روی
                     شارژ تأییدشده/برگشت‌خورده/تعارض بانکی و یک شارژ کارت‌به‌کارت برای خطای نوع پرداخت

.EXAMPLE
    .\seed.ps1 -Action up

.EXAMPLE
    .\seed.ps1 -Action verify

.EXAMPLE
    .\seed.ps1 -Action reset -AllowDatabaseReset

.EXAMPLE
    .\seed.ps1 -Action buyers

.EXAMPLE
    .\seed.ps1 -Action shipping-flags -PurchaseId "<guid>" -CarrierFlag return

.EXAMPLE
    .\seed.ps1 -Action finance

.EXAMPLE
    .\seed.ps1 -Action gateway-review
#>
[CmdletBinding()]
param(
    [ValidateSet('up', 'verify', 'reset', 'buyers', 'shipping-flags', 'finance', 'gateway-review')]
    [string]$Action = 'up',

    [string]$PurchaseId = '',
    [ValidateSet('return', 'alert')]
    [string]$CarrierFlag = 'return',
    [ValidateSet(11304, 11305)]
    [int]$AlertCode = 11304,

    [string]$StrapiUrl = 'http://localhost:1337',
    [string]$ByeMoneyUrl = 'http://localhost:5000',

    # ادمین محلی استرپی؛ فقط برای ساخت دادهٔ پایه (دوره، کالا، کوپن، تنظیمات ارسال).
    [string]$StrapiAdminEmail = $env:SEED_STRAPI_ADMIN_EMAIL,
    [string]$StrapiAdminPassword = $env:SEED_STRAPI_ADMIN_PASSWORD,

    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    [string]$PgHost = 'localhost',
    [int]$PgPort = 5432,
    [string]$PgUser = 'postgres',
    [string]$ByeMoneyDatabase = 'ByeMoney_Dev',
    [string]$StrapiDatabase = 'tarhelahi_server',

    [switch]$AllowDatabaseReset
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:StateDirectory = Join-Path $script:RepoRoot '.artifacts\manual-test'
$script:StatePath = Join-Path $script:StateDirectory 'seed-state.json'
$script:BackendEnvPath = Join-Path (Split-Path -Parent $script:RepoRoot) 'backend\.env'

# حساب‌های ثابت تست؛ تکرارپذیری سید به همین شناسه‌ها وابسته است.
$script:SeedUsers = @(
    [pscustomobject]@{ Key = 'funded';  Phone = '09120000001'; Password = 'Manual-Test-2026'; FirstName = 'کاربر'; LastName = 'دارای موجودی'; IsAdmin = $false; IsBlocked = $false; TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'empty';   Phone = '09120000002'; Password = 'Manual-Test-2026'; FirstName = 'کاربر'; LastName = 'بدون موجودی';  IsAdmin = $false; IsBlocked = $false; TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'admin';   Phone = '09120000003'; Password = 'Manual-Test-2026'; FirstName = 'ادمین';  LastName = 'تست';           IsAdmin = $true;  IsBlocked = $false; TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'blocked'; Phone = '09120000004'; Password = 'Manual-Test-2026'; FirstName = 'کاربر'; LastName = 'مسدود';         IsAdmin = $false; IsBlocked = $true;  TargetBalanceNoor = 0 }
)

$script:SeedGift = [pscustomobject]@{
    IdempotencyKey = 'manual-test-seed-gift-0001'
    AmountNoor     = 5
    Reason         = 'هدیهٔ تست دستی'
}

# شناسه‌های ثابت فصل‌های دورهٔ فصلی؛ checkout بای‌مانی فصل را با همین UUID می‌خواند.
$script:SeasonalChapterIds = @('a0000001-0000-4000-8000-00000000c001', 'a0000002-0000-4000-8000-00000000c002')

# دستهٔ خریداران سناریوی «سبد و خرید»؛ هر نقش در سناریو خرید می‌کند و مالکیت برگشت‌ناپذیر است،
# پس هر اجرای سناریو به کاربر تازه نیاز دارد. موجودی هدف از مسیر شارژ و تأیید ادمین تأمین می‌شود.
$script:CartBuyerRoles = @(
    [pscustomobject]@{ Key = 'courseBuyer';    Title = 'خریدار دوره';           TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'couponBuyer';    Title = 'خریدار کوپن';           TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'chapterBuyer';   Title = 'خریدار فصل';            TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'freeBuyer';      Title = 'خریدار رایگان';         TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'productBuyer';   Title = 'خریدار کالا';           TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'mixedBuyer';     Title = 'خریدار سبد مختلط';      TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'shortfallBuyer';       Title = 'خریدار کم‌موجودی';          TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'stockBuyer';           Title = 'خریدار موجودی کالا';        TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'manualPaidBuyer';      Title = 'خریدار سفارش دستی پولی';    TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'manualFreeBuyer';      Title = 'خریدار سفارش دستی رایگان';  TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'manualShortfallBuyer'; Title = 'خریدار کم‌موجودی دستی';     TargetBalanceNoor = 0 }
    [pscustomobject]@{ Key = 'shippingDeliveryBuyer'; Title = 'خریدار ارسال و تحویل';      TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'shippingReturnBuyer';   Title = 'خریدار برگشتی ارسال';       TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'shippingAlertBuyer';    Title = 'خریدار هشدار پستی';        TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'cancelBuyer';           Title = 'خریدار لغو کل سبد';         TargetBalanceNoor = 150; TargetGiftNoor = 0 }
    [pscustomobject]@{ Key = 'cancelPartialBuyer';    Title = 'خریدار لغو جزئی و هدیه';    TargetBalanceNoor = 100; TargetGiftNoor = 10 }
    [pscustomobject]@{ Key = 'cancelShippedBuyer';    Title = 'خریدار لغو پس از ارسال';    TargetBalanceNoor = 100; TargetGiftNoor = 0 }
    [pscustomobject]@{ Key = 'settlementBuyer';       Title = 'خریدار تسویهٔ سفارش';       TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'withdrawalBuyer';       Title = 'خریدار خروج نور';           TargetBalanceNoor = 100 }
    [pscustomobject]@{ Key = 'gatewayReviewBuyer';    Title = 'خریدار رسیدگی درگاه';       TargetBalanceNoor = 0 }
)

$script:AdminRoleId = '11111111-1111-1111-1111-111111111111'
$script:AdminToken = $null
$script:PgPasswordRestore = $false

#region زیرساخت مشترک

function Write-Step {
    param([string]$Message)
    Write-Host "[..] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[ok] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!]  $Message" -ForegroundColor Yellow
}

function Assert-LoopbackUrl {
    param([string]$Url, [string]$Name)

    $uri = [uri]$Url
    $allowed = @('localhost', '127.0.0.1', '::1')

    if ($allowed -notcontains $uri.Host) {
        throw "$Name با میزبان '$($uri.Host)' تنظیم شده است. این اسکریپت فقط روی میزبان لوکال (localhost/127.0.0.1) اجرا می‌شود."
    }
}

function Assert-LoopbackHost {
    param([string]$HostName)

    if (@('localhost', '127.0.0.1', '::1') -notcontains $HostName) {
        throw "میزبان دیتابیس '$HostName' لوکال نیست. اسکریپت روی دیتابیس غیرلوکال اجرا نمی‌شود."
    }
}

function Assert-DevDatabaseName {
    param([string]$DatabaseName)

    if ($DatabaseName -notmatch '(_Dev|_Test|_Local)$') {
        throw "نام دیتابیس '$DatabaseName' الگوی توسعه (_Dev/_Test/_Local) را ندارد؛ عملیات روی آن انجام نمی‌شود."
    }
}

function Invoke-Http {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        $Body,
        [int]$TimeoutSec = 30
    )

    $request = @{
        Uri             = $Uri
        Method          = $Method
        TimeoutSec      = $TimeoutSec
        UseBasicParsing = $true
    }

    if ($null -ne $Headers) {
        $request.Headers = $Headers
    }

    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 12))
    }

    try {
        $response = Invoke-WebRequest @request
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content    = $response.Content
            Data       = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json }
        }
    }
    catch {
        $statusCode = 0
        $content = $_.Exception.Message

        if ($null -ne $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
                $reader.Dispose()
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Content    = $content
            Data       = $null
        }
    }
}

function Invoke-HttpMultipart {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        [hashtable]$Fields,
        [int]$TimeoutSec = 30
    )

    $boundary = [guid]::NewGuid().ToString('N')
    $builder = New-Object System.Text.StringBuilder
    foreach ($name in $Fields.Keys) {
        $null = $builder.Append("--$boundary`r`n")
        $null = $builder.Append("Content-Disposition: form-data; name=`"$name`"`r`n`r`n")
        $null = $builder.Append([string]$Fields[$name])
        $null = $builder.Append("`r`n")
    }
    $null = $builder.Append("--$boundary--`r`n")

    $request = @{
        Uri             = $Uri
        Method          = $Method
        TimeoutSec      = $TimeoutSec
        UseBasicParsing = $true
        ContentType     = "multipart/form-data; boundary=$boundary"
        Body            = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
    }

    if ($null -ne $Headers) {
        $request.Headers = $Headers
    }

    try {
        $response = Invoke-WebRequest @request
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content    = $response.Content
            Data       = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json }
        }
    }
    catch {
        $statusCode = 0
        $content = $_.Exception.Message

        if ($null -ne $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
                $reader.Dispose()
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Content    = $content
            Data       = $null
        }
    }
}

function Read-EnvValue {
    param([string]$Path, [string]$Key)

    $line = Select-String -Path $Path -Pattern "^$Key=(.*)$" | Select-Object -First 1
    if ($null -eq $line) {
        return $null
    }

    return $line.Matches.Groups[1].Value.Trim().Trim('"')
}

function Get-SqlPassword {
    if (-not [string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
        return $env:PGPASSWORD
    }

    if (Test-Path $script:BackendEnvPath) {
        $fromBackend = Read-EnvValue -Path $script:BackendEnvPath -Key 'DATABASE_PASSWORD'
        if (-not [string]::IsNullOrWhiteSpace($fromBackend)) {
            return $fromBackend
        }
    }

    throw 'رمز دیتابیس پیدا نشد. متغیر محیطی PGPASSWORD را تنظیم کنید.'
}

function Invoke-Psql {
    param(
        [string]$Database,
        [string]$Sql,
        [switch]$AllowFailure
    )

    $script:PgPasswordRestore = $true
    $env:PGPASSWORD = Get-SqlPassword

    $tempFile = Join-Path $env:TEMP ("manual-test-{0}.sql" -f ([guid]::NewGuid().ToString('N')))
    [System.IO.File]::WriteAllText($tempFile, $Sql, (New-Object System.Text.UTF8Encoding($false)))

    try {
        # پیام‌های NOTICE روی stderr می‌آیند و نباید اجرا را متوقف کنند؛ تنها کد خروج معیار است.
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & $PsqlPath -h $PgHost -p $PgPort -U $PgUser -d $Database -t -A -F '|' -v ON_ERROR_STOP=1 -f $tempFile 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousPreference
        }
    }
    finally {
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    }

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "اجرای SQL روی دیتابیس '$Database' شکست خورد: $($output -join ' ')"
    }

    return $output
}

function Test-ByeMoneyHealth {
    $probe = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/settings/conversion-rate"
    return (($probe.StatusCode -ge 200 -and $probe.StatusCode -le 299))
}

function Test-StrapiHealth {
    $probe = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/_health"
    return ($probe.StatusCode -eq 204 -or ($probe.StatusCode -ge 200 -and $probe.StatusCode -le 299))
}

function Assert-ServicesRunning {
    if (-not (Test-StrapiHealth)) {
        throw "استرپی روی $StrapiUrl پاسخ نمی‌دهد. ابتدا npm run dev را در backend اجرا کنید."
    }

    if (-not (Test-ByeMoneyHealth)) {
        throw "بای‌مانی روی $ByeMoneyUrl پاسخ نمی‌دهد. ابتدا dotnet run --project src/ByeMoney.API را اجرا کنید."
    }
}

#endregion

#region استرپی: کاربران و توکن ادمین

function Get-StrapiAdminToken {
    if ($null -ne $script:AdminToken) {
        return $script:AdminToken
    }

    if (-not [string]::IsNullOrWhiteSpace($env:SEED_STRAPI_ADMIN_TOKEN)) {
        $script:AdminToken = $env:SEED_STRAPI_ADMIN_TOKEN
        return $script:AdminToken
    }

    if ([string]::IsNullOrWhiteSpace($StrapiAdminEmail) -or [string]::IsNullOrWhiteSpace($StrapiAdminPassword)) {
        return $null
    }

    # ورود ادمین استرپی محدودیت نرخ دارد؛ یک تلاش مجدد کوتاه انجام می‌شود.
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        $login = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/admin/login" -Body @{
            email    = $StrapiAdminEmail
            password = $StrapiAdminPassword
        }

        if ($login.StatusCode -ge 200 -and $login.StatusCode -le 299) {
            $script:AdminToken = $login.Data.data.token
            return $script:AdminToken
        }

        if ($login.StatusCode -ne 429 -or $attempt -eq 2) {
            Write-Warn "ورود ادمین استرپی ناموفق بود ($($login.StatusCode)). دادهٔ پایه ساخته نمی‌شود."
            return $null
        }

        Write-Warn 'ورود ادمین استرپی محدود شده است (429)؛ ۳۰ ثانیه بعد دوباره تلاش می‌شود.'
        Start-Sleep -Seconds 30
    }

    return $null
}

function Get-AdminHeaders {
    $token = Get-StrapiAdminToken
    if ($null -eq $token) {
        return $null
    }

    return @{ Authorization = "Bearer $token" }
}

function Connect-SeedUser {
    param([pscustomobject]$User)

    $register = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/register" -Body @{
        phoneNumber = $User.Phone
        password    = $User.Password
        firstName   = $User.FirstName
        lastName    = $User.LastName
    }

    if ($register.StatusCode -ge 200 -and $register.StatusCode -le 299) {
        Write-Ok "کاربر $($User.Phone) در استرپی ساخته شد."
        return $register.Data
    }

    $login = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
        phoneNumber = $User.Phone
        password    = $User.Password
    }

    if ($login.StatusCode -lt 200 -or $login.StatusCode -gt 299) {
        throw "کاربر $($User.Phone) در استرپی هست ولی با رمز سید وارد نشد ($($login.StatusCode)). این شماره را در استرپی بررسی کنید: $($login.Content)"
    }

    Write-Ok "کاربر $($User.Phone) از قبل بود و با رمز سید وارد شد."
    return $login.Data
}

function Sync-ByeMoneyUser {
    param([pscustomobject]$User, [string]$StrapiJwt)

    $sync = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/auth/sync" -Headers @{ Authorization = "Bearer $StrapiJwt" } -Body @{}
    if ($sync.StatusCode -lt 200 -or $sync.StatusCode -gt 299) {
        throw "همگام‌سازی کاربر $($User.Phone) در بای‌مانی ناموفق بود ($($sync.StatusCode)): $($sync.Content)"
    }

    return [pscustomobject]@{
        InternalUserId = $sync.Data.userId
        StrapiJwt      = $StrapiJwt
    }
}

#endregion

#region بای‌مانی: نقش، موجودی، هدیه

function Set-AdminRole {
    param([string]$InternalUserId, [string]$Phone)

    $sql = @"
INSERT INTO "UserRoles" ("UserId", "RoleId", "CreatedAt")
VALUES ('$InternalUserId', '$script:AdminRoleId', (now() AT TIME ZONE 'utc'))
ON CONFLICT ("UserId", "RoleId") DO NOTHING;
"@

    Invoke-Psql -Database $ByeMoneyDatabase -Sql $sql | Out-Null
    Write-Ok "نقش Admin به کاربر $Phone داده شد (فقط جدول RBAC)."
}

function Set-StrapiAdminRole {
    param([string]$Phone)

    $sql = @"
DO `$`$
DECLARE
    v_user_id integer;
    v_role_id integer;
BEGIN
    SELECT id INTO v_user_id FROM up_users WHERE username = '$Phone' LIMIT 1;
    SELECT id INTO v_role_id FROM up_roles WHERE type = 'administrator' LIMIT 1;
    IF v_user_id IS NOT NULL AND v_role_id IS NOT NULL THEN
        UPDATE up_users_role_lnk SET role_id = v_role_id WHERE user_id = v_user_id;
        IF NOT FOUND THEN
            INSERT INTO up_users_role_lnk (user_id, role_id) VALUES (v_user_id, v_role_id);
        END IF;
    END IF;
END `$`$;
"@

    Invoke-Psql -Database $StrapiDatabase -Sql $sql | Out-Null
    Write-Ok "نقش administrator در استرپی به کاربر $Phone داده شد (برای پنل ادمین و admin-preview)."
}

function Get-WalletBalance {
    param([string]$StrapiJwt)

    $balance = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/wallet/balance" -Headers @{ Authorization = "Bearer $StrapiJwt" }
    if ($balance.StatusCode -lt 200 -or $balance.StatusCode -gt 299) {
        throw "خواندن موجودی ناموفق بود ($($balance.StatusCode)): $($balance.Content)"
    }

    return [decimal]$balance.Data.balance
}

function Set-TargetBalance {
    param([pscustomobject]$User, [string]$StrapiJwt, [string]$AdminJwt, [decimal]$TargetBalanceNoor)

    $current = Get-WalletBalance -StrapiJwt $StrapiJwt
    $shortfall = $TargetBalanceNoor - $current

    if ($shortfall -le 0) {
        Write-Ok "موجودی $($User.Phone) برابر $current نور است؛ شارژ لازم نیست."
        return
    }

    $create = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/topup/requests" -Headers @{ Authorization = "Bearer $StrapiJwt" } -Body @{
        amount        = [decimal]$shortfall
        paymentMethod = 2
    }

    if ($create.StatusCode -lt 200 -or $create.StatusCode -gt 299) {
        throw "ساخت درخواست شارژ برای $($User.Phone) ناموفق بود ($($create.StatusCode)): $($create.Content)"
    }

    $topUpId = $create.Data.topUpRequestId
    $confirm = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/admin/topups/$topUpId/confirm" -Headers @{ Authorization = "Bearer $AdminJwt" } -Body @{
        externalTransactionId = "MANUAL-TEST-$($User.Phone)-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        confirmedAmount       = [decimal]$shortfall
    }

    if ($confirm.StatusCode -ne 204) {
        throw "تأیید شارژ $($User.Phone) ناموفق بود ($($confirm.StatusCode)): $($confirm.Content)"
    }

    $after = Get-WalletBalance -StrapiJwt $StrapiJwt
    Write-Ok "شارژ $shortfall نور برای $($User.Phone) تأیید شد؛ موجودی فعلی $after نور."
}

function Test-SeedGiftAlreadyGranted {
    $sql = 'SELECT count(*) FROM "GiftGrantBatches" WHERE "IdempotencyKey" = ''' + $script:SeedGift.IdempotencyKey + ''';'
    $rows = Invoke-Psql -Database $ByeMoneyDatabase -Sql $sql
    return ([int]($rows | Select-Object -First 1) -gt 0)
}

function Grant-SeedGift {
    param([string]$ExternalUserId, [string]$AdminJwt, [string]$Phone)

    if (Test-SeedGiftAlreadyGranted) {
        Write-Ok "هدیهٔ تست قبلاً برای $Phone ثبت شده بود (بر اساس کلید Idempotency)."
        return
    }

    $gift = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/admin/noor-gifts" -Headers @{
        Authorization   = "Bearer $AdminJwt"
        'Idempotency-Key' = $script:SeedGift.IdempotencyKey
    } -Body @{
        externalUserIds = @($ExternalUserId)
        amountNoor      = $script:SeedGift.AmountNoor
        reason          = $script:SeedGift.Reason
    }

    if ($gift.StatusCode -ge 200 -and $gift.StatusCode -le 299) {
        Write-Ok "هدیهٔ $($script:SeedGift.AmountNoor) نور به $Phone داده شد."
        return
    }

    Write-Warn "ثبت هدیهٔ تست برای $Phone انجام نشد ($($gift.StatusCode)): $($gift.Content)"
}

#endregion

#region دادهٔ پایه در استرپی

function Find-AdminRecord {
    param([string]$CollectionUid, [string]$FilterField, [string]$FilterValue)

    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return $null
    }

    $query = "filters[$FilterField][`$eq]=" + [uri]::EscapeDataString($FilterValue)

    # در Strapi ۵ فهرست پیش‌فرض فقط نسخهٔ منتشرشده را می‌دهد؛ نسخهٔ پیش‌نویس هم بررسی می‌شود
    # تا اجرای دوباره رکورد تکراری نسازد.
    foreach ($status in @('published', 'draft')) {
        $response = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/${CollectionUid}?$query&pageSize=1&status=$status" -Headers $headers
        if ($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) {
            throw "جست‌وجوی $CollectionUid ناموفق بود ($($response.StatusCode)): $($response.Content)"
        }

        if ($response.Data.pagination.total -ge 1) {
            return $response.Data.results[0]
        }
    }

    return $null
}

function Test-RecordPublished {
    param($Record)

    if ($null -eq $Record) {
        return $false
    }

    if ($null -eq $Record.PSObject.Properties['publishedAt']) {
        return $false
    }

    return ($null -ne $Record.publishedAt)
}

function Publish-AdminRecord {
    param([string]$CollectionUid, [string]$DocumentId)

    $headers = Get-AdminHeaders
    $publish = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/content-manager/collection-types/$CollectionUid/$DocumentId/actions/publish" -Headers $headers -Body @{}
    if ($publish.StatusCode -ge 200 -and $publish.StatusCode -le 299) {
        return $publish.Data.data
    }

    # بعضی محتواها با وجود انتشار موفق، پاسخ خطا می‌دهند؛ وضعیت واقعی دوباره خوانده می‌شود.
    $check = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/$CollectionUid/${DocumentId}?status=published" -Headers $headers
    if (($check.StatusCode -ge 200 -and $check.StatusCode -le 299) -and (Test-RecordPublished $check.Data.data)) {
        Write-Warn "انتشار $CollectionUid/$DocumentId با کد $($publish.StatusCode) پاسخ داد ولی رکورد منتشر شده است."
        return $check.Data.data
    }

    throw "انتشار $CollectionUid/$DocumentId ناموفق بود ($($publish.StatusCode)): $($publish.Content)"
}

function Ensure-Course {
    param(
        [string]$Slug,
        [string]$Title,
        [decimal]$Price,
        [bool]$IsChaptered,
        $Chapters
    )

    $existing = Find-AdminRecord -CollectionUid 'api::course.course' -FilterField 'slug' -FilterValue $Slug
    if ($null -ne $existing) {
        if (-not (Test-RecordPublished $existing)) {
            Publish-AdminRecord -CollectionUid 'api::course.course' -DocumentId $existing.documentId | Out-Null
            Write-Ok "دورهٔ '$Title' از قبل بود و منتشر شد."
        }
        else {
            Write-Ok "دورهٔ '$Title' از قبل بود."
        }

        return $existing.documentId
    }

    $headers = Get-AdminHeaders
    $body = [ordered]@{
        title       = $Title
        slug        = $Slug
        price       = $Price
        isFree      = $false
        isChaptered = $IsChaptered
    }

    if ($null -ne $Chapters) {
        $body['chapters'] = $Chapters
    }

    $create = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/content-manager/collection-types/api::course.course" -Headers $headers -Body $body
    if ($create.StatusCode -lt 200 -or $create.StatusCode -gt 299) {
        throw "ساخت دورهٔ '$Title' ناموفق بود ($($create.StatusCode)): $($create.Content)"
    }

    $documentId = $create.Data.data.documentId
    Publish-AdminRecord -CollectionUid 'api::course.course' -DocumentId $documentId | Out-Null
    Write-Ok "دورهٔ '$Title' ساخته و منتشر شد."
    return $documentId
}

function Get-ExistingProductImageId {
    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return $null
    }

    $response = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/api::product.product?pageSize=10" -Headers $headers
    if (($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) -or $null -eq $response.Data) {
        return $null
    }

    foreach ($item in @($response.Data.results)) {
        if ($null -eq $item -or $null -eq $item.PSObject.Properties['images'] -or $null -eq $item.images) {
            continue
        }

        $first = @($item.images)[0]
        if ($null -ne $first -and $null -ne $first.PSObject.Properties['id']) {
            return $first.id
        }
    }

    return $null
}

function Ensure-ProductImage {
    param([string]$Slug)

    $headers = Get-AdminHeaders
    $search = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/upload/files?filters[name][`$containsi]=$(($Slug -replace '[^a-zA-Z0-9]', '_'))" -Headers $headers
    if (($search.StatusCode -ge 200 -and $search.StatusCode -le 299) -and $null -ne $search.Data) {
        # Strapi 5 فهرست فایل را با results/pagination برمی‌گرداند؛ پاسخ قدیمی خودش آرایه بود.
        $files = if ($null -ne $search.Data.PSObject.Properties['results']) { @($search.Data.results) } else { @($search.Data) }
        $hit = @($files | Where-Object { $null -ne $_ -and $null -ne $_.PSObject.Properties['id'] })
        if ($hit.Count -gt 0) {
            return $hit[0].id
        }
    }

    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap 320, 200
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(238, 238, 238))
    $graphics.DrawString('MANUAL TEST PRODUCT', (New-Object System.Drawing.Font('Arial', 14)), [System.Drawing.Brushes]::DimGray, 12, 90)
    $graphics.Dispose()

    $imagePath = Join-Path $env:TEMP ("$Slug.png")
    $bitmap.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()

    try {
        $upload = & curl.exe -s -X POST "$StrapiUrl/upload" -H "Authorization: Bearer $(Get-StrapiAdminToken)" -F "files=@$imagePath" -F "refId=$Slug" -F "field=manual-test"
        $uploaded = $null
        try {
            $uploaded = $upload | ConvertFrom-Json
        }
        catch {
            $uploaded = $null
        }

        # پاسخ موفق آرایهٔ فایل است؛ پاسخ خطا شیئی با error است.
        if ($null -ne $uploaded -and $uploaded -isnot [System.Array] -and $null -ne $uploaded.PSObject.Properties['error']) {
            $uploaded = $null
        }

        if ($null -ne $uploaded) {
            return @($uploaded)[0].id
        }

        # توکن پنل ادمین روی /upload در این بیلد پذیرفته نمی‌شود؛ از تصویر کالای موجود استفاده می‌شود.
        $existing = Get-ExistingProductImageId
        if ($null -ne $existing) {
            Write-Warn 'بارگذاری تصویر کالا انجام نشد؛ تصویر یک کالای موجود به کالای جدید داده می‌شود.'
            return $existing
        }

        Write-Warn 'بارگذاری تصویر کالا ناموفق بود.'
        return $null
    }
    finally {
        Remove-Item $imagePath -Force -ErrorAction SilentlyContinue
    }
}

function Get-BoxTypeId {
    $schemaPath = Join-Path $script:RepoRoot '..\backend\src\api\product\content-types\product\schema.json'
    $schema = Get-Content -Path $schemaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $box = $schema.attributes.boxTypeId.enum | Where-Object { $_ -like 'box5*' } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($box)) {
        throw 'مقدار جعبهٔ box5 در schema کالا پیدا نشد.'
    }

    return $box
}

function Ensure-Product {
    param(
        [string]$Slug,
        [string]$Title,
        [decimal]$Price,
        [int]$Stock,
        [string]$ShippingOriginDocumentId
    )

    $existing = Find-AdminRecord -CollectionUid 'api::product.product' -FilterField 'slug' -FilterValue $Slug
    if ($null -ne $existing) {
        if (-not (Test-RecordPublished $existing)) {
            Publish-AdminRecord -CollectionUid 'api::product.product' -DocumentId $existing.documentId | Out-Null
            Write-Ok "کالای '$Title' از قبل بود و منتشر شد."
        }
        else {
            Write-Ok "کالای '$Title' از قبل بود."
        }

        return $existing.documentId
    }

    if ([string]::IsNullOrWhiteSpace($ShippingOriginDocumentId)) {
        Write-Warn "مبدأ ارسال پیدا نشد؛ کالای '$Title' ساخته نمی‌شود."
        return $null
    }

    $headers = Get-AdminHeaders
    $imageId = Ensure-ProductImage -Slug $Slug
    if ($null -eq $imageId) {
        Write-Warn "تصویر کالای '$Title' در دسترس نیست؛ کالا ساخته نشد."
        return $null
    }

    $body = [ordered]@{
        title          = $Title
        slug           = $Slug
        price          = $Price
        stock          = $Stock
        weight         = 200
        isAvailable    = $true
        isFragile      = $false
        boxTypeId      = Get-BoxTypeId
        images         = @(@{ id = $imageId })
        shippingOrigin = @(@{ documentId = $ShippingOriginDocumentId })
    }

    $create = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/content-manager/collection-types/api::product.product" -Headers $headers -Body $body
    if ($create.StatusCode -lt 200 -or $create.StatusCode -gt 299) {
        throw "ساخت کالای '$Title' ناموفق بود ($($create.StatusCode)): $($create.Content)"
    }

    $documentId = $create.Data.data.documentId
    Publish-AdminRecord -CollectionUid 'api::product.product' -DocumentId $documentId | Out-Null
    Write-Ok "کالای '$Title' با موجودی $Stock ساخته و منتشر شد."
    return $documentId
}

function Ensure-Coupon {
    param([string]$Code, [string]$Title, [int]$Percent, [int]$MaxUsage = 0)

    $existing = Find-AdminRecord -CollectionUid 'api::coupon.coupon' -FilterField 'code' -FilterValue $Code
    if ($null -ne $existing) {
        Write-Ok "کوپن $Code از قبل بود."
        return $existing.documentId
    }

    $headers = Get-AdminHeaders
    $body = [ordered]@{
        code                = $Code
        title               = $Title
        discountType        = 'percentage'
        discountValue       = $Percent
        appliesToAllCourses = $true
        isActive            = $true
    }

    # سقف مصرف فقط وقتی ثبت می‌شود که سناریو به ظرفیت محدود نیاز داشته باشد.
    if ($MaxUsage -gt 0) {
        $body['maxUsage'] = $MaxUsage
    }

    $create = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/content-manager/collection-types/api::coupon.coupon" -Headers $headers -Body $body
    if ($create.StatusCode -lt 200 -or $create.StatusCode -gt 299) {
        throw "ساخت کوپن $Code ناموفق بود ($($create.StatusCode)): $($create.Content)"
    }

    $documentId = $create.Data.data.documentId
    # این محتوا «draftAndPublish» ندارد و با ساخت منتشر می‌شود؛ فراخوانی publish روی آن پاسخ ۵۰۰ می‌دهد.
    Write-Ok "کوپن $Code ساخته شد."
    return $documentId
}

function Get-ShippingOrigin {
    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return $null
    }

    $response = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/api::shipping-origin.shipping-origin?pageSize=5" -Headers $headers
    if (($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) -or $response.Data.pagination.total -lt 1) {
        Write-Warn 'مبدأ ارسال در استرپی پیدا نشد؛ کالای فیزیکی بدون مبدأ ساخته نمی‌شود.'
        return $null
    }

    return $response.Data.results[0]
}

function Show-ShippingSetting {
    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return
    }

    $response = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/single-types/api::shipping-setting.shipping-setting" -Headers $headers
    if (($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) -or $null -eq $response.Data.data) {
        Write-Warn 'تنظیمات ارسال خوانده نشد؛ آن را دستی در پنل استرپی بسازید.'
        return
    }

    $setting = $response.Data.data
    Write-Ok ("تنظیمات ارسال: costMode=$($setting.costMode)، collectionType=$($setting.collectionType)، fixedAmountInNoor=$($setting.fixedAmountInNoor)، freeShippingThresholdInNoor=$($setting.freeShippingThresholdInNoor)، rialPerNoorForShipping=$($setting.rialPerNoorForShipping)")

    if ($setting.costMode -eq 'postex') {
        Write-Warn 'costMode روی postex است؛ هر تست کالای فیزیکی هزینهٔ ارسال را از پستکس می‌پرسد (فراخوانی واقعی).'
    }
    elseif ($null -eq $setting.fixedAmountInNoor -or "$($setting.fixedAmountInNoor)" -eq '') {
        Write-Warn 'costMode روی fixed است ولی fixedAmountInNoor تنظیم نشده؛ سبد دارای کالا با تعارض رد می‌شود.'
    }
}

function Get-BearerHeaders {
    param([string]$Token)

    return @{ Authorization = 'Bearer ' + $Token }
}

function Get-FirstCity {
    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return $null
    }

    $response = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/api::city.city?pageSize=1" -Headers $headers
    if (($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) -or $response.Data.pagination.total -lt 1) {
        Write-Warn 'هیچ شهری در استرپی پیدا نشد؛ آدرس خریداران ساخته نمی‌شود و سبد کالای فیزیکی آماده نمی‌شود.'
        return $null
    }

    return $response.Data.results[0]
}

function Set-UserAddress {
    param([int]$StrapiUserId, [string]$Phone, $City)

    $headers = Get-AdminHeaders
    if ($null -eq $headers) {
        return $false
    }

    $query = 'filters[user][id][$eq]=' + $StrapiUserId
    $existing = Invoke-Http -Method 'GET' -Uri "$StrapiUrl/content-manager/collection-types/api::address.address?$query&pageSize=1" -Headers $headers
    if (($existing.StatusCode -ge 200 -and $existing.StatusCode -le 299) -and $existing.Data.pagination.total -ge 1) {
        $record = $existing.Data.results[0]
        if (-not (Test-RecordPublished $record)) {
            Publish-AdminRecord -CollectionUid 'api::address.address' -DocumentId $record.documentId | Out-Null
            Write-Ok "آدرس $Phone از قبل بود و منتشر شد."
        }
        else {
            Write-Ok "آدرس $Phone از قبل بود."
        }

        return $true
    }

    if ($null -eq $City) {
        return $false
    }

    # نام استان و شهر متن نمایشی آدرس است؛ مبلغ ارسال شهر را از رابطهٔ cityLink می‌خواند.
    $provinceName = ''
    if ($null -ne $City.PSObject.Properties['province'] -and $null -ne $City.province -and
        $null -ne $City.province.PSObject.Properties['name']) {
        $provinceName = [string]$City.province.name
    }

    $body = [ordered]@{
        title          = 'آزمون دستی'
        province       = $provinceName
        city           = [string]$City.name
        postalCode     = '0000000000'
        fullAddress    = 'آزمون دستی — خیابان نمونه، کوچهٔ آزمایش، پلاک ۱'
        recipientName  = 'خریدار آزمون دستی'
        recipientPhone = $Phone
        user           = @{ id = $StrapiUserId }
        cityLink       = @{ documentId = [string]$City.documentId }
    }

    $create = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/content-manager/collection-types/api::address.address" -Headers $headers -Body $body
    if ($create.StatusCode -lt 200 -or $create.StatusCode -gt 299) {
        throw "ساخت آدرس $Phone ناموفق بود ($($create.StatusCode)): $($create.Content)"
    }

    Publish-AdminRecord -CollectionUid 'api::address.address' -DocumentId $create.Data.data.documentId | Out-Null
    Write-Ok "آدرس کامل برای $Phone ساخته و منتشر شد (شهر: $($City.name))."
    return $true
}

function Set-BlockedUser {
    param([string]$Phone)

    $sql = @"
UPDATE up_users
SET blocked = true
WHERE username = '$Phone' AND blocked = false;
"@

    Invoke-Psql -Database $StrapiDatabase -Sql $sql | Out-Null
    Write-Ok "کاربر $Phone در استرپی مسدود شد (فقط پرچم کاربر، بدون اثر مالی)."
}

#endregion

#region بررسی

function Test-SeedState {
    param([hashtable]$Sessions, $Fixtures, $Buyers = $null)

    $results = New-Object System.Collections.Generic.List[object]
    $failed = 0

    foreach ($user in $script:SeedUsers) {
        if ($user.Key -eq 'blocked') {
            continue
        }

        try {
            $jwt = $Sessions[$user.Key].StrapiJwt
            $balanceResponse = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/wallet/balance" -Headers @{ Authorization = "Bearer $jwt" }
            if ($balanceResponse.StatusCode -lt 200 -or $balanceResponse.StatusCode -gt 299) {
                throw "کد $($balanceResponse.StatusCode)"
            }

            $transactions = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/wallet/transactions" -Headers @{ Authorization = "Bearer $jwt" }
            $count = if (($transactions.StatusCode -ge 200 -and $transactions.StatusCode -le 299)) { @($transactions.Data.items).Count } else { 0 }

            $note = "موجودی $($balanceResponse.Data.balance) نور، $count تراکنش"
            if ($user.TargetBalanceNoor -gt 0 -and [decimal]$balanceResponse.Data.balance -lt $user.TargetBalanceNoor) {
                throw "موجودی کمتر از هدف: $($balanceResponse.Data.balance) < $($user.TargetBalanceNoor)"
            }

            $results.Add([pscustomobject]@{ مورد = "کیف پول $($user.Phone)"; وضعیت = 'موفق'; توضیح = $note })
        }
        catch {
            $failed++
            $results.Add([pscustomobject]@{ مورد = "کیف پول $($user.Phone)"; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
        }
    }

    try {
        $adminJwt = $Sessions['admin'].StrapiJwt
        $permissions = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/admin/topups/permissions" -Headers @{ Authorization = "Bearer $adminJwt" }
        if ($permissions.StatusCode -lt 200 -or $permissions.StatusCode -gt 299) {
            throw "کد $($permissions.StatusCode)"
        }

        $permissionCount = @($permissions.Data.permissions).Count
        if ($permissionCount -lt 8) {
            throw "تعداد مجوز $permissionCount کمتر از ۸ است"
        }

        $results.Add([pscustomobject]@{ مورد = 'مجوزهای ادمین'; وضعیت = 'موفق'; توضیح = "$permissionCount مجوز، نقش Admin" })
    }
    catch {
        $failed++
        $results.Add([pscustomobject]@{ مورد = 'مجوزهای ادمین'; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
    }

    try {
        $blockedLogin = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
            phoneNumber = '09120000004'
            password    = 'Manual-Test-2026'
        }

        if ($blockedLogin.StatusCode -ge 200 -and $blockedLogin.StatusCode -le 299) {
            throw 'ورود کاربر مسدود پذیرفته شد'
        }

        $results.Add([pscustomobject]@{ مورد = 'کاربر مسدود'; وضعیت = 'موفق'; توضیح = "ورود با کد $($blockedLogin.StatusCode) رد شد" })
    }
    catch {
        $failed++
        $results.Add([pscustomobject]@{ مورد = 'کاربر مسدود'; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
    }

    $courseDocumentId = $null
    if ($null -ne $Fixtures) {
        if ($Fixtures -is [System.Collections.IDictionary]) {
            $courseDocumentId = $Fixtures['basicCourseDocumentId']
        }
        elseif ($null -ne $Fixtures.PSObject.Properties['basicCourseDocumentId']) {
            $courseDocumentId = $Fixtures.basicCourseDocumentId
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($courseDocumentId)) {
        try {
            $jwt = $Sessions['funded'].StrapiJwt
            $checkout = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/courses/$courseDocumentId/checkout" -Headers @{ Authorization = "Bearer $jwt" }
            if ($checkout.StatusCode -lt 200 -or $checkout.StatusCode -gt 299) {
                throw "کد $($checkout.StatusCode)"
            }

            $results.Add([pscustomobject]@{ مورد = 'دورهٔ پایه از دید بای‌مانی'; وضعیت = 'موفق'; توضیح = "قیمت $($checkout.Data.priceInNoor) نور" })
        }
        catch {
            $failed++
            $results.Add([pscustomobject]@{ مورد = 'دورهٔ پایه از دید بای‌مانی'; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
        }
    }

    if ($null -ne (Get-AdminHeaders)) {
        try {
            $limited = Find-AdminRecord -CollectionUid 'api::coupon.coupon' -FilterField 'code' -FilterValue 'MANUALTEST1'
            if ($null -eq $limited) {
                throw 'کوپن MANUALTEST1 ساخته نشده است؛ -Action up را با ادمین استرپی اجرا کنید.'
            }

            $usedCount = [int]$limited.usedCount
            if ($usedCount -ne 0) {
                throw "usedCount=$usedCount؛ ظرفیت کوپن درگیر است (سبد رهاشده تا ۳۰ دقیقه بعد خودکار آزاد می‌شود)."
            }

            $results.Add([pscustomobject]@{ مورد = 'کوپن تک‌نفرهٔ MANUALTEST1'; وضعیت = 'موفق'; توضیح = 'usedCount=0، ظرفیت آزاد' })
        }
        catch {
            $failed++
            $results.Add([pscustomobject]@{ مورد = 'کوپن تک‌نفرهٔ MANUALTEST1'; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
        }
    }

    # دستهٔ خریداران سناریوی «سبد و خرید»: موجودی هدف، بدون خرید قبلی و دارای آدرس کامل.
    foreach ($buyer in @($Buyers)) {
        if ($null -eq $buyer) {
            continue
        }

        $label = "خریدار $($buyer.phone) ($($buyer.title))"
        try {
            $buyerLogin = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
                phoneNumber = $buyer.phone
                password    = $buyer.password
            }
            if ($buyerLogin.StatusCode -lt 200 -or $buyerLogin.StatusCode -gt 299) {
                throw "ورود ناموفق ($($buyerLogin.StatusCode))"
            }

            $buyerHeaders = Get-BearerHeaders -Token $buyerLogin.Data.jwt
            $balanceResponse = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/wallet/balance" -Headers $buyerHeaders
            if ($balanceResponse.StatusCode -lt 200 -or $balanceResponse.StatusCode -gt 299) {
                throw "خواندن موجودی ناموفق ($($balanceResponse.StatusCode))"
            }

            $balance = [decimal]$balanceResponse.Data.balance
            if ($balance -lt [decimal]$buyer.targetBalanceNoor) {
                throw "موجودی $balance کمتر از هدف $($buyer.targetBalanceNoor)"
            }

            $targetGift = if ($null -ne $buyer.PSObject.Properties['targetGiftNoor']) { [decimal]$buyer.targetGiftNoor } else { 0 }
            $giftBalance = if ($null -ne $balanceResponse.Data.PSObject.Properties['giftBalance']) { [decimal]$balanceResponse.Data.giftBalance } else { 0 }
            if ($targetGift -gt 0 -and $giftBalance -lt $targetGift) {
                throw "موجودی هدیهٔ $giftBalance کمتر از هدف $targetGift"
            }

            $note = if ($targetGift -gt 0) { "موجودی $balance نور (هدیه $giftBalance)" } else { "موجودی $balance نور" }
            if (-not [string]::IsNullOrWhiteSpace($courseDocumentId)) {
                $owned = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/courses/$courseDocumentId/checkout" -Headers $buyerHeaders
                $purchaseStatus = [string]$owned.Data.purchaseStatus
                if ($purchaseStatus -ne 'NotPurchased') {
                    throw "دورهٔ پایه قبلاً خریده شده است (purchaseStatus=$purchaseStatus)؛ دسته مصرف شده، -Action buyers را دوباره اجرا کنید."
                }

                $note += '، بدون خرید قبلی'
            }

            if (-not $buyer.hasAddress) {
                throw 'آدرس کامل ندارد؛ سبد کالای فیزیکی آماده نمی‌شود'
            }

            $results.Add([pscustomobject]@{ مورد = $label; وضعیت = 'موفق'; توضیح = $note })
        }
        catch {
            $failed++
            $results.Add([pscustomobject]@{ مورد = $label; وضعیت = 'ناموفق'; توضیح = $_.Exception.Message })
        }
    }

    $results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

    if ($failed -gt 0) {
        Write-Warn "$failed بررسی ناموفق بود. پذیرش را اعلام نکنید."
        return $false
    }

    Write-Ok 'همهٔ بررسی‌های API موفق بود.'
    return $true
}

#endregion

#region اقدام‌ها

function Get-BlockedSeedUser {
    param([string]$Phone)

    $strapiRows = Invoke-Psql -Database $StrapiDatabase -Sql "SELECT document_id || '|' || blocked::text FROM up_users WHERE username = '$Phone' LIMIT 1;"
    if ($null -eq $strapiRows -or [string]::IsNullOrWhiteSpace([string]$strapiRows)) {
        return $null
    }

    $parts = ([string]$strapiRows).Split('|')
    if ($parts[1] -ne 'true') {
        return $null
    }

    $externalUserId = $parts[0]
    $internalSql = 'SELECT "Id" FROM "Users" WHERE "ExternalUserId" = ''' + $externalUserId + ''' LIMIT 1;'
    $internalRows = Invoke-Psql -Database $ByeMoneyDatabase -Sql $internalSql

    return [pscustomobject]@{
        ExternalUserId = $externalUserId
        InternalUserId = ([string]$internalRows).Trim()
    }
}

function Invoke-SeedUp {
    Assert-ServicesRunning

    $sessions = @{}
    $fixtures = [ordered]@{}
    $adminJwt = $null

    # شناسه‌های قبلی حفظ می‌شوند تا اجرای بدون ادمین، شناسه‌های دادهٔ پایه را پاک نکند.
    $previousBuyers = $null
    if (Test-Path $script:StatePath) {
        $previousState = Get-Content -Path $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $previousState.fixtures) {
            foreach ($property in $previousState.fixtures.PSObject.Properties) {
                $fixtures[$property.Name] = $property.Value
            }
        }

        # دستهٔ خریداران نسخهٔ جدا دارد و نباید با up پاک شود.
        if ($null -ne $previousState.PSObject.Properties['buyers']) {
            $previousBuyers = $previousState.buyers
        }
    }

    Write-Step 'ساخت/ورود کاربران تست از مسیر استرپی (بدون پیامک)'
    foreach ($user in $script:SeedUsers) {
        if ($user.IsBlocked) {
            $blockedState = Get-BlockedSeedUser -Phone $user.Phone
            if ($null -ne $blockedState) {
                Write-Ok "کاربر $($user.Phone) از قبل مسدود است (وضعیت مورد انتظار)؛ ورود و همگام‌سازی لازم نیست."
                $sessions[$user.Key] = [pscustomobject]@{
                    InternalUserId = $blockedState.InternalUserId
                    StrapiJwt      = $null
                    ExternalUserId = $blockedState.ExternalUserId
                }
                continue
            }
        }

        $strapiUser = Connect-SeedUser -User $user
        $session = Sync-ByeMoneyUser -User $user -StrapiJwt $strapiUser.jwt
        $session | Add-Member -NotePropertyName ExternalUserId -NotePropertyValue $strapiUser.user.documentId
        $sessions[$user.Key] = $session
        # شناسهٔ عددی کاربر در استرپی برای ربط دادن رکورد آدرس به او
        $session | Add-Member -NotePropertyName StrapiNumericId -NotePropertyValue $strapiUser.user.id

        if ($user.IsAdmin) {
            $adminJwt = $session.StrapiJwt
        }
    }

    Write-Step 'تخصیص نقش Admin در بای‌مانی و administrator در استرپی'
    Set-AdminRole -InternalUserId $sessions['admin'].InternalUserId -Phone '09120000003'
    Set-StrapiAdminRole -Phone '09120000003'

    Write-Step 'تأمین موجودی کاربر دارای موجودی از مسیر شارژ و تأیید ادمین'
    Set-TargetBalance -User $script:SeedUsers[0] -StrapiJwt $sessions['funded'].StrapiJwt -AdminJwt $adminJwt -TargetBalanceNoor $script:SeedUsers[0].TargetBalanceNoor

    Write-Step 'ثبت هدیهٔ نور از مسیر ادمین'
    Grant-SeedGift -ExternalUserId $sessions['funded'].ExternalUserId -AdminJwt $adminJwt -Phone '09120000001'

    Write-Step 'آدرس کامل خریداران (پیش‌نیاز سبد کالای فیزیکی)'
    if ($null -eq (Get-AdminHeaders)) {
        Write-Warn 'اطلاعات ادمین استرپی داده نشده است؛ آدرس ساخته نمی‌شود.'
    }
    else {
        $city = Get-FirstCity
        foreach ($user in $script:SeedUsers) {
            if ($user.IsBlocked) {
                continue
            }

            $null = Set-UserAddress -StrapiUserId $sessions[$user.Key].StrapiNumericId -Phone $user.Phone -City $city
        }
    }

    Write-Step 'دادهٔ پایه در استرپی'
    if ($null -eq (Get-AdminHeaders)) {
        Write-Warn 'اطلاعات ادمین استرپی داده نشده است؛ مرحلهٔ دادهٔ پایه رد شد.'
        Write-Warn 'برای ساخت آن: npm run strapi admin:create-user -e seed@local.dev -p <password> -f Seed -l Admin'
        Write-Warn 'سپس: .\seed.ps1 -Action up -StrapiAdminEmail seed@local.dev -StrapiAdminPassword <password>'
    }
    else {
        $fixtures.basicCourseDocumentId = Ensure-Course -Slug 'manual-test-course-basic' -Title 'آزمون دستی — دورهٔ پایه' -Price 20 -IsChaptered $false -Chapters $null
        $fixtures.seasonalChapter1IntegrationId = $script:SeasonalChapterIds[0]
        $fixtures.seasonalChapter2IntegrationId = $script:SeasonalChapterIds[1]
        $fixtures.seasonalCourseDocumentId = Ensure-Course -Slug 'manual-test-course-seasonal' -Title 'آزمون دستی — دورهٔ فصلی' -Price 0 -IsChaptered $true -Chapters @(
            [ordered]@{ title = 'فصل ۱'; price = 10; integrationId = $script:SeasonalChapterIds[0] }
            [ordered]@{ title = 'فصل ۲'; price = 10; integrationId = $script:SeasonalChapterIds[1] }
        )
        $fixtures.couponDocumentId = Ensure-Coupon -Code 'MANUALTEST10' -Title 'کوپن تست دستی' -Percent 10
        # کوپن تک‌نفره فقط برای سناریوی ظرفیت و آزادسازی رزرو؛ مصرف قطعی‌اش ظرفیت را برای همیشه می‌بندد.
        $fixtures.limitedCouponDocumentId = Ensure-Coupon -Code 'MANUALTEST1' -Title 'کوپن تک‌نفرهٔ تست دستی' -Percent 10 -MaxUsage 1

        $origin = Get-ShippingOrigin
        if ($null -ne $origin) {
            $fixtures.shippingOriginDocumentId = $origin.documentId
            Write-Ok "مبدأ ارسال: $($origin.displayName) ($($origin.originId))"
            $fixtures.physicalProductDocumentId = Ensure-Product -Slug 'manual-test-physical-product' -Title 'آزمون دستی — کالای فیزیکی' -Price 30 -Stock 10 -ShippingOriginDocumentId $origin.documentId
            # موجودی ۱: سناریوی کسری موجودی کالا با درخواست تعداد ۲ رد می‌شود.
            $fixtures.lowStockProductDocumentId = Ensure-Product -Slug 'manual-test-product-low-stock' -Title 'آزمون دستی — کالای کم‌موجودی' -Price 5 -Stock 1 -ShippingOriginDocumentId $origin.documentId
        }

        Show-ShippingSetting
    }

    Write-Step 'مسدودسازی کاربر نمونه (آخرین مرحله، چون ورودش را می‌بندد)'
    Set-BlockedUser -Phone '09120000004'

    Write-Step 'بررسی داده از مسیر API'
    $verified = Test-SeedState -Sessions $sessions -Fixtures $fixtures

    if (-not (Test-Path $script:StateDirectory)) {
        New-Item -ItemType Directory -Path $script:StateDirectory -Force | Out-Null
    }

    $state = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        strapiUrl      = $StrapiUrl
        byeMoneyUrl    = $ByeMoneyUrl
        users          = @(
            foreach ($user in $script:SeedUsers) {
                [ordered]@{
                    key            = $user.Key
                    phone          = $user.Phone
                    password       = $user.Password
                    externalUserId = $sessions[$user.Key].ExternalUserId
                    internalUserId = $sessions[$user.Key].InternalUserId
                    isAdmin        = $user.IsAdmin
                    isBlocked      = $user.IsBlocked
                }
            }
        )
        fixtures       = $fixtures
        verified       = $verified
    }

    if ($null -ne $previousBuyers) {
        $state['buyers'] = $previousBuyers
    }

    $state | ConvertTo-Json -Depth 8 | Set-Content -Path $script:StatePath -Encoding UTF8
    Write-Ok "شناسه‌های ساخته‌شده در $script:StatePath ثبت شد."
}

function Invoke-SeedVerify {
    Assert-ServicesRunning

    if (-not (Test-Path $script:StatePath)) {
        throw "فایل شناسه‌ها نیست ($script:StatePath). ابتدا -Action up را اجرا کنید."
    }

    $state = Get-Content -Path $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $sessions = @{}

    foreach ($user in $state.users) {
        if ($user.isBlocked) {
            continue
        }

        $login = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
            phoneNumber = $user.phone
            password    = $user.password
        }

        if ($login.StatusCode -lt 200 -or $login.StatusCode -gt 299) {
            throw "ورود $($user.phone) ناموفق بود ($($login.StatusCode))."
        }

        $sessions[$user.key] = [pscustomobject]@{ StrapiJwt = $login.Data.jwt }
    }

    $null = Test-SeedState -Sessions $sessions -Fixtures $state.fixtures -Buyers (Get-BuyerBatch -State $state)
}

function Get-BuyerBatch {
    param($State)

    if ($null -eq $State -or $null -eq $State.PSObject.Properties['buyers'] -or $null -eq $State.buyers) {
        return $null
    }

    return @($State.buyers.items)
}

function Invoke-SeedBuyers {
    Assert-ServicesRunning

    if (-not (Test-Path $script:StatePath)) {
        throw "فایل شناسه‌ها نیست ($script:StatePath). ابتدا -Action up را اجرا کنید."
    }

    $state = Get-Content -Path $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$state.fixtures.basicCourseDocumentId)) {
        throw 'دادهٔ پایه (دوره، کالا، کوپن) ساخته نشده است. ابتدا -Action up را با ادمین استرپی اجرا کنید.'
    }

    # تأیید شارژ فقط از مسیر ادمین است؛ ورود با رمز کاربر ادمین نمونه انجام می‌شود.
    $adminUser = $script:SeedUsers | Where-Object { $_.Key -eq 'admin' }
    $adminLogin = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
        phoneNumber = $adminUser.Phone
        password    = $adminUser.Password
    }
    if ($adminLogin.StatusCode -lt 200 -or $adminLogin.StatusCode -gt 299) {
        throw "ورود کاربر ادمین ($($adminUser.Phone)) ناموفق بود ($($adminLogin.StatusCode))."
    }

    $city = Get-FirstCity

    # شماره‌های آزاد با یک خواندن فقط‌خواندنی از استرپی پیدا می‌شوند؛ ورودهای ناموفق پشت‌سرهم ساخته نمی‌شوند.
    $usedPhones = @(Invoke-Psql -Database $StrapiDatabase -Sql "SELECT username FROM up_users WHERE username LIKE '091200000__';") |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $freePhones = @(@(11..99) | ForEach-Object { '091200000{0:D2}' -f $_ } | Where-Object { $usedPhones -notcontains $_ })
    if ($freePhones.Count -lt $script:CartBuyerRoles.Count) {
        throw "شمارهٔ آزاد برای ساخت خریداران کافی نیست ($($freePhones.Count) عدد)."
    }

    Write-Step "ساخت دستهٔ تازهٔ خریداران ($($script:CartBuyerRoles.Count) کاربر)"
    $buyers = New-Object System.Collections.Generic.List[object]
    for ($index = 0; $index -lt $script:CartBuyerRoles.Count; $index++) {
        $role = $script:CartBuyerRoles[$index]
        $phone = $freePhones[$index]
        $profile = [pscustomobject]@{ Key = $role.Key; Phone = $phone; FirstName = 'خریدار'; LastName = $role.Title }

        $register = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/register" -Body @{
            phoneNumber = $phone
            password    = $script:SeedUsers[0].Password
            firstName   = $profile.FirstName
            lastName    = $profile.LastName
        }
        if ($register.StatusCode -lt 200 -or $register.StatusCode -gt 299) {
            throw "ثبت‌نام خریدار $phone ناموفق بود ($($register.StatusCode)): $($register.Content)"
        }

        $session = Sync-ByeMoneyUser -User $profile -StrapiJwt $register.Data.jwt
        Set-TargetBalance -User $profile -StrapiJwt $register.Data.jwt -AdminJwt $adminLogin.Data.jwt -TargetBalanceNoor $role.TargetBalanceNoor

        $targetGift = if ($null -ne $role.PSObject.Properties['TargetGiftNoor']) { [decimal]$role.TargetGiftNoor } else { 0 }
        if ($targetGift -gt 0) {
            $giftResp = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/admin/noor-gifts" -Headers @{
                Authorization     = "Bearer $($adminLogin.Data.jwt)"
                'Idempotency-Key' = "manual-test-buyer-gift-$($role.Key)-$($register.Data.user.documentId)"
            } -Body @{
                externalUserIds = @([string]$register.Data.user.documentId)
                amountNoor      = $targetGift
                reason          = 'هدیهٔ تست دستی لغو جزئی'
            }
            if ($giftResp.StatusCode -lt 200 -or $giftResp.StatusCode -gt 299) {
                throw "ثبت هدیه برای خریدار $phone ناموفق بود ($($giftResp.StatusCode)): $($giftResp.Content)"
            }
            Write-Ok "هدیهٔ $targetGift نور به $phone داده شد."
        }

        $hasAddress = $false
        if ($null -ne $city) {
            $hasAddress = Set-UserAddress -StrapiUserId ([int]$register.Data.user.id) -Phone $phone -City $city
        }

        $buyers.Add([pscustomobject]@{
            key               = $role.Key
            title             = $role.Title
            phone             = $phone
            password          = $script:SeedUsers[0].Password
            externalUserId    = [string]$register.Data.user.documentId
            internalUserId    = $session.InternalUserId
            targetBalanceNoor = $role.TargetBalanceNoor
            targetGiftNoor    = $targetGift
            hasAddress        = $hasAddress
        })
    }

    $state | Add-Member -NotePropertyName buyers -NotePropertyValue ([pscustomobject]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        items          = $buyers.ToArray()
    }) -Force
    $state | ConvertTo-Json -Depth 8 | Set-Content -Path $script:StatePath -Encoding UTF8
    Write-Ok "دستهٔ خریداران در $script:StatePath ثبت شد (buyers.items)."

    $sessions = @{}
    foreach ($user in $state.users) {
        if ($user.isBlocked) {
            continue
        }

        $login = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
            phoneNumber = $user.phone
            password    = $user.password
        }
        if ($login.StatusCode -lt 200 -or $login.StatusCode -gt 299) {
            throw "ورود $($user.phone) ناموفق بود ($($login.StatusCode))."
        }

        $sessions[$user.key] = [pscustomobject]@{ StrapiJwt = $login.Data.jwt }
    }

    Write-Step 'بررسی دستهٔ خریداران از مسیر API'
    $null = Test-SeedState -Sessions $sessions -Fixtures $state.fixtures -Buyers (Get-BuyerBatch -State $state)
}

function Get-AdminUserJwt {
    $admin = $script:SeedUsers | Where-Object { $_.Key -eq 'admin' }
    $login = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
        phoneNumber = $admin.Phone
        password    = $admin.Password
    }

    if ($login.StatusCode -lt 200 -or $login.StatusCode -gt 299) {
        throw "ورود کاربر ادمین ($($admin.Phone)) ناموفق بود ($($login.StatusCode))."
    }

    return [string]$login.Data.jwt
}

# دادهٔ سناریوی «تسویه و خروج نور»: یک شارژ دارای نرخ (برای استرداد پرداخت تکراری) و یک شارژ بدون نرخ
# روی همان خریدار خروج. سفارش‌های قابل‌تسویه در خود سناریو از مسیر سفارش دستی ادمین ساخته می‌شوند تا هر
# اجرا دادهٔ تازه داشته باشد.
function Invoke-SeedFinance {
    Assert-ServicesRunning

    if (-not (Test-Path $script:StatePath)) {
        throw "فایل شناسه‌ها نیست ($script:StatePath). ابتدا -Action up را اجرا کنید."
    }

    $state = Get-Content -Path $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$state.fixtures.basicCourseDocumentId)) {
        throw 'دادهٔ پایه (دوره، کالا، کوپن) ساخته نشده است. ابتدا -Action up را با ادمین استرپی اجرا کنید.'
    }

    $buyers = @(Get-BuyerBatch -State $state)
    $settlementBuyer = $buyers | Where-Object { $_.key -eq 'settlementBuyer' } | Select-Object -First 1
    $withdrawalBuyer = $buyers | Where-Object { $_.key -eq 'withdrawalBuyer' } | Select-Object -First 1
    if ($null -eq $settlementBuyer -or $null -eq $withdrawalBuyer) {
        throw 'دستهٔ خریداران نقش settlementBuyer/withdrawalBuyer را ندارد؛ ابتدا -Action buyers را اجرا کنید.'
    }

    $adminJwt = Get-AdminUserJwt
    $adminHeaders = Get-BearerHeaders -Token $adminJwt

    $rateResponse = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/settings/conversion-rate"
    if ($rateResponse.StatusCode -lt 200 -or $rateResponse.StatusCode -gt 299) {
        throw "خواندن نرخ تبدیل ناموفق بود ($($rateResponse.StatusCode)): $($rateResponse.Content)"
    }

    $rate = [decimal]$rateResponse.Data.rialPerNoor
    if ($rate -le 0) {
        throw "نرخ تبدیل معتبر نیست ($rate)."
    }

    # شارژ دارای نرخ: نور و ریال از نرخ فعلی حساب می‌شوند. کلید تکرار به کاربر بسته است تا
    # اجرای دوبارهٔ سید شارژ تازه نسازد.
    $ratedNoor = [decimal]20
    $ratedRial = $ratedNoor * $rate
    $ratedToman = $ratedRial / 10
    $ratedKey = "manual-test-finance-rated-$($withdrawalBuyer.externalUserId)"

    Write-Step "ساخت شارژ دارای نرخ برای $($withdrawalBuyer.phone) — $ratedNoor نور = $ratedRial ریال"
    $rated = Invoke-HttpMultipart -Method 'POST' -Uri "$ByeMoneyUrl/api/admin/topups/assisted" -Headers @{
        Authorization=[redacted]
        'Idempotency-Key' = $ratedKey
    } -Fields @{
        beneficiaryExternalUserId = [string]$withdrawalBuyer.externalUserId
        amountToman               = [string]$ratedToman
    }
    if ($rated.StatusCode -lt 200 -or $rated.StatusCode -gt 299) {
        throw "ساخت شارژ دارای نرخ ناموفق بود ($($rated.StatusCode)): $($rated.Content)"
    }

    $ratedTopUpId = [string]$rated.Data.id
    $ratedCode = [string]$rated.Data.clientReferenceCode
    Write-Ok "شارژ دارای نرخ ثبت شد: $ratedTopUpId (کد $ratedCode)."

    # شارژ بدون نرخ: همان کاربر از مسیر عادی کارت‌به‌کارت؛ برای آزمون خطای NOOR_WITHDRAWAL_TOPUP_INVALID.
    $buyerLogin = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
        phoneNumber = $withdrawalBuyer.phone
        password    = $withdrawalBuyer.password
    }
    if ($buyerLogin.StatusCode -lt 200 -or $buyerLogin.StatusCode -gt 299) {
        throw "ورود $($withdrawalBuyer.phone) ناموفق بود ($($buyerLogin.StatusCode))."
    }

    $buyerJwt = [string]$buyerLogin.Data.jwt
    $reusable = $null -ne $state.PSObject.Properties['finance'] -and
        $null -ne $state.finance -and
        [string]$state.finance.batchAtUtc -eq [string]$state.buyers.generatedAtUtc

    $existingWithdrawal = if ($reusable) {
        @($state.finance.items) | Where-Object { $_.key -eq 'withdrawalBuyer' } | Select-Object -First 1
    }
    else { $null }

    if ($null -ne $existingWithdrawal -and -not [string]::IsNullOrWhiteSpace([string]$existingWithdrawal.unratedTopUpId)) {
        $unratedTopUpId = [string]$existingWithdrawal.unratedTopUpId
        Write-Ok "شارژ بدون نرخ همین دسته از قبل ثبت شده است: $unratedTopUpId."
    }
    else {
        Write-Step "ساخت شارژ بدون نرخ برای $($withdrawalBuyer.phone)"
        $unratedRequest = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/topup/requests" -Headers (Get-BearerHeaders -Token $buyerJwt) -Body @{
            amount        = [decimal]5
            paymentMethod = 2
        }
        if ($unratedRequest.StatusCode -lt 200 -or $unratedRequest.StatusCode -gt 299) {
            throw "ساخت درخواست شارژ بدون نرخ ناموفق بود ($($unratedRequest.StatusCode)): $($unratedRequest.Content)"
        }

        $unratedTopUpId = [string]$unratedRequest.Data.topUpRequestId
        $unratedConfirm = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/admin/topups/$unratedTopUpId/confirm" -Headers $adminHeaders -Body @{
            externalTransactionId = "MANUAL-TEST-FINANCE-$unratedTopUpId"
            confirmedAmount       = [decimal]5
        }
        if ($unratedConfirm.StatusCode -ne 204) {
            throw "تأیید شارژ بدون نرخ ناموفق بود ($($unratedConfirm.StatusCode)): $($unratedConfirm.Content)"
        }

        Write-Ok "شارژ بدون نرخ تأیید شد: $unratedTopUpId."
    }

    $balance = Get-WalletBalance -StrapiJwt $buyerJwt

    Write-Step 'بررسی فهرست شارژهای قابل استرداد از مسیر API'
    $refundable = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/admin/noor-withdrawals/refundable-topups?userExternalId=$([uri]::EscapeDataString([string]$withdrawalBuyer.externalUserId))" -Headers $adminHeaders
    if ($refundable.StatusCode -lt 200 -or $refundable.StatusCode -gt 299) {
        throw "خواندن فهرست قابل استرداد ناموفق بود ($($refundable.StatusCode)): $($refundable.Content)"
    }

    $refundableItems = @($refundable.Data)
    if ($null -eq ($refundableItems | Where-Object { [string]$_.id -eq $ratedTopUpId })) {
        Write-Warn 'شارژ دارای نرخ در فهرست قابل استرداد دیده نشد؛ وضعیت تأییدشده و نرخ ثبت‌شده را بررسی کنید.'
    }
    else {
        Write-Ok 'شارژ دارای نرخ در فهرست قابل استرداد دیده می‌شود.'
    }

    if ($null -ne ($refundableItems | Where-Object { [string]$_.id -eq $unratedTopUpId })) {
        Write-Warn 'شارژ بدون نرخ هم در فهرست قابل استرداد آمد؛ انتظار می‌رفت نباشد.'
    }
    else {
        Write-Ok 'شارژ بدون نرخ در فهرست قابل استرداد نیست (درست).'
    }

    $financeItems = @(
        [pscustomobject]@{
            key            = 'settlementBuyer'
            phone          = [string]$settlementBuyer.phone
            externalUserId = [string]$settlementBuyer.externalUserId
            internalUserId = [string]$settlementBuyer.internalUserId
            balanceNoor    = $null
        }
        [pscustomobject]@{
            key                  = 'withdrawalBuyer'
            phone                = [string]$withdrawalBuyer.phone
            externalUserId       = [string]$withdrawalBuyer.externalUserId
            internalUserId       = [string]$withdrawalBuyer.internalUserId
            balanceNoor          = $balance
            ratedTopUpId         = $ratedTopUpId
            ratedTopUpCode       = $ratedCode
            ratedTopUpAmountNoor = $ratedNoor
            ratedTopUpAmountRial = $ratedRial
            unratedTopUpId       = $unratedTopUpId
            rialPerNoor          = $rate
        }
    )

    $state | Add-Member -NotePropertyName finance -NotePropertyValue ([pscustomobject]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        batchAtUtc     = [string]$state.buyers.generatedAtUtc
        rialPerNoor    = $rate
        items          = $financeItems
    }) -Force
    $state | ConvertTo-Json -Depth 8 | Set-Content -Path $script:StatePath -Encoding UTF8

    $financeItems | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
    Write-Ok "دادهٔ «تسویه و خروج نور» در $script:StatePath ثبت شد (finance.items)."
}

function Get-GatewayServiceKey {
    if (-not [string]::IsNullOrWhiteSpace($env:STRAPI_TO_BYEMONEY_SERVICE_KEY)) {
        return $env:STRAPI_TO_BYEMONEY_SERVICE_KEY.Trim()
    }
    if (Test-Path $script:BackendEnvPath) {
        $fromEnv = Read-EnvValue -Path $script:BackendEnvPath -Key 'STRAPI_TO_BYEMONEY_SERVICE_KEY'
        if (-not [string]::IsNullOrWhiteSpace($fromEnv)) {
            return $fromEnv.Trim()
        }
    }
    $devSettingsPath = Join-Path $script:RepoRoot 'src\ByeMoney.API\appsettings.Development.json'
    if (Test-Path $devSettingsPath) {
        $devSettings = Get-Content -Path $devSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace([string]$devSettings.STRAPI_TO_BYEMONEY_SERVICE_KEY)) {
            return ([string]$devSettings.STRAPI_TO_BYEMONEY_SERVICE_KEY).Trim()
        }
    }
    throw 'کلید سرویس STRAPI_TO_BYEMONEY_SERVICE_KEY در env، backend/.env یا appsettings.Development.json یافت نشد.'
}

function Invoke-SeedGatewayReview {
    Assert-ServicesRunning

    if (-not (Test-Path $script:StatePath)) {
        throw "فایل شناسه‌ها نیست ($script:StatePath). ابتدا -Action up را اجرا کنید."
    }

    $state = Get-Content -Path $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $buyers = if ($null -ne $state.PSObject.Properties['buyers'] -and $null -ne $state.buyers) { @(Get-BuyerBatch -State $state) } else { @() }
    $reviewBuyer = $buyers | Where-Object { $_.key -eq 'gatewayReviewBuyer' } | Select-Object -First 1
    $batchKey = if ($null -ne $reviewBuyer) { [string]$state.buyers.generatedAtUtc } else { 'default-empty-user' }
    if ($null -eq $reviewBuyer) {
        $emptyUser = @($state.users) | Where-Object { $_.key -eq 'empty' } | Select-Object -First 1
        if ($null -eq $emptyUser) {
            throw 'کاربر نمونهٔ empty (09120000002) در seed-state.json نیست؛ ابتدا -Action up را اجرا کنید.'
        }
        $reviewBuyer = [pscustomobject]@{
            key            = 'empty'
            phone          = [string]$emptyUser.phone
            password       = 'Manual-Test-2026'
            externalUserId = [string]$emptyUser.externalUserId
            internalUserId = [string]$emptyUser.internalUserId
        }
    }

    $reusable = $null -ne $state.PSObject.Properties['gatewayReview'] -and
        $null -ne $state.gatewayReview -and
        [string]$state.gatewayReview.batchAtUtc -eq $batchKey
    if ($reusable) {
        Write-Ok "دادهٔ «رسیدگی مالی درگاه» برای همین دسته از قبل ساخته شده است ($script:StatePath)."
        @($state.gatewayReview.items) | Format-Table key, clientReferenceCode, amountNoor, amountRial, status, caseId -AutoSize | Out-String -Width 200 | Write-Host
        return
    }

    $rateResponse = Invoke-Http -Method 'GET' -Uri "$ByeMoneyUrl/api/settings/conversion-rate"
    if ($rateResponse.StatusCode -lt 200 -or $rateResponse.StatusCode -gt 299) {
        throw "خواندن نرخ تبدیل ناموفق بود ($($rateResponse.StatusCode)): $($rateResponse.Content)"
    }
    $rate = [decimal]$rateResponse.Data.rialPerNoor
    if ($rate -le 0) {
        throw "نرخ تبدیل معتبر نیست ($rate)."
    }

    $buyerLogin = Invoke-Http -Method 'POST' -Uri "$StrapiUrl/api/auth/password/login" -Body @{
        phoneNumber = $reviewBuyer.phone
        password    = $reviewBuyer.password
    }
    if ($buyerLogin.StatusCode -lt 200 -or $buyerLogin.StatusCode -gt 299) {
        throw "ورود $($reviewBuyer.phone) ناموفق بود ($($buyerLogin.StatusCode))."
    }
    $buyerJwt = [string]$buyerLogin.Data.jwt
    $buyerHeaders = Get-BearerHeaders -Token $buyerJwt
    $serviceKey = Get-GatewayServiceKey
    $serviceHeaders = @{ 'X-Service-Key' = $serviceKey }
    $nowUtc = (Get-Date).ToUniversalTime().ToString('o')

    $newTopUp = {
        param([decimal]$amount, [int]$method)
        $resp = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/topup/requests" -Headers $buyerHeaders -Body @{
            amount        = $amount
            paymentMethod = $method
        }
        if ($resp.StatusCode -lt 200 -or $resp.StatusCode -gt 299) {
            throw "ساخت درخواست شارژ ($amount نور، روش $method) ناموفق بود ($($resp.StatusCode)): $($resp.Content)"
        }
        return $resp.Data
    }

    Write-Step "ساخت ۵ درخواست شارژ برای $($reviewBuyer.phone) جهت سناریوی رسیدگی مالی درگاه"

    # ۱) شارژ معلق درگاه (بدون نتیجه و بدون پرونده)
    $pending = & $newTopUp 10 1

    # ۲) شارژ درگاه تأییدشده + پروندهٔ باز (برای PAID_AND_CONFIRMED و خطای MANUAL_REFUND)
    $paid = & $newTopUp 11 1
    $paidRef = [string]$paid.clientReferenceId
    $paidBankTx = "MT-GW-VER-$paidRef"
    $paidBankRrn = "RRN-PAID-$paidRef"
    $paidRial = [decimal]11 * $rate
    $r1 = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/gateway-results" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $paidRef
        gateway             = 'SEP'
        eventId             = "evt-seed-paid-$paidRef"
        kind                = 'Verified'
        bankTransactionId   = $paidBankTx
        bankReferenceNumber = $paidBankRrn
        bankResultCode      = '0'
        originalAmountRial  = $paidRial
        affectiveAmountRial = $paidRial
        occurredAtUtc       = $nowUtc
    }
    if ($r1.StatusCode -ne 200) { throw "ثبت نتیجه Verified برای paidCaseTopUp ناموفق بود ($($r1.StatusCode)): $($r1.Content)" }
    $paidCaseId = "case-paid-$paidRef"
    $o1 = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/v1/gateway-reviews/open" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $paidRef
        caseId              = $paidCaseId
        reasonCode          = 'VERIFY_UNKNOWN'
    }
    if ($o1.StatusCode -ne 200) { throw "بازکردن پرونده paidCaseTopUp ناموفق بود ($($o1.StatusCode)): $($o1.Content)" }

    # ۳) شارژ درگاه برگشت‌خورده + پروندهٔ باز (برای REVERSED_REJECTED)
    $reversed = & $newTopUp 12 1
    $revRef = [string]$reversed.clientReferenceId
    $revBankTx = "MT-GW-REV-$revRef"
    $revBankRrn = "RRN-REV-$revRef"
    $revRial = [decimal]12 * $rate
    $r2 = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/gateway-results" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $revRef
        gateway             = 'SEP'
        eventId             = "evt-seed-rev-$revRef"
        kind                = 'ReverseSucceeded'
        bankTransactionId   = $revBankTx
        bankReferenceNumber = $revBankRrn
        bankResultCode      = '5'
        originalAmountRial  = $revRial
        affectiveAmountRial = $revRial
        occurredAtUtc       = $nowUtc
    }
    if ($r2.StatusCode -ne 200) { throw "ثبت نتیجه ReverseSucceeded ناموفق بود ($($r2.StatusCode)): $($r2.Content)" }
    $revCaseId = "case-rev-$revRef"
    $o2 = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/v1/gateway-reviews/open" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $revRef
        caseId              = $revCaseId
        reasonCode          = 'REVERSE_UNKNOWN'
    }
    if ($o2.StatusCode -ne 200) { throw "بازکردن پرونده reversedCaseTopUp ناموفق بود ($($o2.StatusCode)): $($o2.Content)" }

    # ۴) شارژ ردشده + Verified دیرهنگام -> پروندهٔ خودکار BANK_CONFLICT (برای CONFIRMED_EXCEPTIONAL)
    $exceptional = & $newTopUp 13 1
    $excRef = [string]$exceptional.clientReferenceId
    $excBankTx = "MT-GW-EXC-$excRef"
    $excBankRrn = "RRN-EXC-$excRef"
    $excRial = [decimal]13 * $rate
    $r3a = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/gateway-results" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $excRef
        gateway             = 'SEP'
        eventId             = "evt-seed-exc-unpaid-$excRef"
        kind                = 'Unpaid'
        bankResultCode      = '-2'
        occurredAtUtc       = $nowUtc
    }
    if ($r3a.StatusCode -ne 200) { throw "ثبت نتیجه Unpaid برای exceptionalCaseTopUp ناموفق بود ($($r3a.StatusCode)): $($r3a.Content)" }
    $r3b = Invoke-Http -Method 'POST' -Uri "$ByeMoneyUrl/api/integrations/topups/gateway-results" -Headers $serviceHeaders -Body @{
        clientReferenceCode = $excRef
        gateway             = 'SEP'
        eventId             = "evt-seed-exc-ver-$excRef"
        kind                = 'Verified'
        bankTransactionId   = $excBankTx
        bankReferenceNumber = $excBankRrn
        bankResultCode      = '0'
        originalAmountRial  = $excRial
        affectiveAmountRial = $excRial
        occurredAtUtc       = $nowUtc
    }
    if ($r3b.StatusCode -ne 200) { throw "ثبت نتیجه Verified روی شارژ ردشده ناموفق بود ($($r3b.StatusCode)): $($r3b.Content)" }

    # ۵) شارژ کارت‌به‌کارت معلق (برای آزمون خطای روش پرداخت غیر از درگاه)
    $cardToCard = & $newTopUp 5 2

    $balance = Get-WalletBalance -StrapiJwt $buyerJwt
    $reviewItems = @(
        [pscustomobject]@{
            key                 = 'pendingTopUp'
            topUpRequestId      = [string]$pending.topUpRequestId
            clientReferenceCode = [string]$pending.clientReferenceId
            amountNoor          = [decimal]10
            amountRial          = [decimal]$pending.amountRial
            status              = 'Pending'
            caseId              = $null
            reasonCode          = $null
            bankTransactionId   = $null
            bankReferenceNumber = $null
        }
        [pscustomobject]@{
            key                 = 'paidCaseTopUp'
            topUpRequestId      = [string]$paid.topUpRequestId
            clientReferenceCode = $paidRef
            amountNoor          = [decimal]11
            amountRial          = $paidRial
            status              = 'Confirmed'
            caseId              = $paidCaseId
            reasonCode          = 'VERIFY_UNKNOWN'
            bankTransactionId   = $paidBankTx
            bankReferenceNumber = $paidBankRrn
        }
        [pscustomobject]@{
            key                 = 'reversedCaseTopUp'
            topUpRequestId      = [string]$reversed.topUpRequestId
            clientReferenceCode = $revRef
            amountNoor          = [decimal]12
            amountRial          = $revRial
            status              = 'Rejected'
            caseId              = $revCaseId
            reasonCode          = 'REVERSE_UNKNOWN'
            bankTransactionId   = $revBankTx
            bankReferenceNumber = $revBankRrn
        }
        [pscustomobject]@{
            key                 = 'exceptionalCaseTopUp'
            topUpRequestId      = [string]$exceptional.topUpRequestId
            clientReferenceCode = $excRef
            amountNoor          = [decimal]13
            amountRial          = $excRial
            status              = 'Rejected'
            caseId              = "case-$excRef"
            reasonCode          = 'BANK_CONFLICT'
            bankTransactionId   = $excBankTx
            bankReferenceNumber = $excBankRrn
        }
        [pscustomobject]@{
            key                 = 'cardToCardTopUp'
            topUpRequestId      = [string]$cardToCard.topUpRequestId
            clientReferenceCode = [string]$cardToCard.clientReferenceId
            amountNoor          = [decimal]5
            amountRial          = [decimal]$cardToCard.amountRial
            status              = 'Pending'
            caseId              = $null
            reasonCode          = $null
            bankTransactionId   = $null
            bankReferenceNumber = $null
        }
    )

    $state | Add-Member -NotePropertyName gatewayReview -NotePropertyValue ([pscustomobject]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        batchAtUtc     = $batchKey
        rialPerNoor    = $rate
        buyer          = [pscustomobject]@{
            phone          = [string]$reviewBuyer.phone
            externalUserId = [string]$reviewBuyer.externalUserId
            internalUserId = [string]$reviewBuyer.internalUserId
            balanceNoor    = $balance
        }
        items          = $reviewItems
    }) -Force
    $state | ConvertTo-Json -Depth 8 | Set-Content -Path $script:StatePath -Encoding UTF8

    $reviewItems | Format-Table key, clientReferenceCode, amountNoor, amountRial, status, caseId -AutoSize | Out-String -Width 200 | Write-Host
    Write-Ok "دادهٔ «رسیدگی مالی درگاه» در $script:StatePath ثبت شد (gatewayReview.items)."
}

function Invoke-SeedReset {
    Assert-DevDatabaseName -DatabaseName $ByeMoneyDatabase

    if (-not $AllowDatabaseReset) {
        throw "برای بازسازی دیتابیس '$ByeMoneyDatabase' سوئیچ -AllowDatabaseReset را اضافه کنید."
    }

    $sql = @"
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$ByeMoneyDatabase' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "$ByeMoneyDatabase" WITH (FORCE);
CREATE DATABASE "$ByeMoneyDatabase";
"@

    Invoke-Psql -Database 'postgres' -Sql $sql | Out-Null
    Write-Ok "دیتابیس '$ByeMoneyDatabase' از نو ساخته شد."

    Write-Warn 'پروسهٔ API را متوقف و دوباره اجرا کنید تا مهاجرت‌ها اعمال شوند، سپس -Action up را اجرا کنید.'
}

function Invoke-SeedShippingFlags {
    Assert-DevDatabaseName -DatabaseName $ByeMoneyDatabase

    $parsedId = [Guid]::Empty
    if (-not [Guid]::TryParse($PurchaseId, [ref]$parsedId) -or $parsedId -eq [Guid]::Empty) {
        throw 'برای -Action shipping-flags باید شناسهٔ معتبر -PurchaseId (GUID) بدهید.'
    }

    $idLiteral = $parsedId.ToString('D')
    $status = (Invoke-Psql -Database $ByeMoneyDatabase -Sql "SELECT ""DeliveryStatus"" FROM ""PhysicalProductPurchases"" WHERE ""Id"" = '$idLiteral';").Trim()
    if ([string]::IsNullOrWhiteSpace($status)) {
        throw "قلم فیزیکی '$idLiteral' در دیتابیس '$ByeMoneyDatabase' پیدا نشد."
    }

    if ($status -ne 'Shipped') {
        throw "ثبت پرچم برگشتی/هشدار فقط روی قلم با وضعیت Shipped مجاز است (وضعیت فعلی: '$status')."
    }

    if ($CarrierFlag -eq 'return') {
        $sql = @"
UPDATE "PhysicalProductPurchases"
SET "CarrierReturnedAtUtc" = COALESCE("CarrierReturnedAtUtc", NOW() AT TIME ZONE 'UTC'),
    "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
WHERE "Id" = '$idLiteral' AND "DeliveryStatus" = 'Shipped';
"@
        Invoke-Psql -Database $ByeMoneyDatabase -Sql $sql | Out-Null
        Write-Ok "پرچم برگشتی (CarrierReturnedAtUtc) روی قلم '$idLiteral' ثبت شد (بدون اثر مالی)."
    }
    else {
        $sql = @"
UPDATE "PhysicalProductPurchases"
SET "CarrierAlertCode" = COALESCE("CarrierAlertCode", $AlertCode),
    "CarrierAlertAtUtc" = COALESCE("CarrierAlertAtUtc", NOW() AT TIME ZONE 'UTC'),
    "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
WHERE "Id" = '$idLiteral' AND "DeliveryStatus" = 'Shipped';
"@
        Invoke-Psql -Database $ByeMoneyDatabase -Sql $sql | Out-Null
        Write-Ok "هشدار پستی (CarrierAlertCode=$AlertCode) روی قلم '$idLiteral' ثبت شد (بدون اثر مالی)."
    }
}

#endregion

try {
    Assert-LoopbackUrl -Url $StrapiUrl -Name 'StrapiUrl'
    Assert-LoopbackUrl -Url $ByeMoneyUrl -Name 'ByeMoneyUrl'
    Assert-LoopbackHost -HostName $PgHost

    switch ($Action) {
        'up' { Invoke-SeedUp }
        'verify' { Invoke-SeedVerify }
        'reset' { Invoke-SeedReset }
        'buyers' { Invoke-SeedBuyers }
        'shipping-flags' { Invoke-SeedShippingFlags }
        'finance' { Invoke-SeedFinance }
        'gateway-review' { Invoke-SeedGatewayReview }
    }
}
catch {
    Write-Host "[x] $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    exit 1
}
finally {
    if ($script:PgPasswordRestore) {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }
}
