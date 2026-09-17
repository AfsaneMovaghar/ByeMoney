-- ============================================================================
-- اسکریپت تخصیص نقش مدیر (Admin) به کاربر استرپی در دیتابیس ByeMoney
-- کاربرد: امکان تأیید و رد درخواست‌های شارژ (TopUp Review) توسط مدیر در پنل
-- ============================================================================

DO $$
DECLARE
    -- ⚠️ شناسه documentId کاربر مدیر در استرپی را در متغیر زیر قرار دهید:
    v_strapi_document_id TEXT := 'PUT_STRAPI_ADMIN_DOCUMENT_ID_HERE';

    v_admin_role_id UUID := '11111111-1111-1111-1111-111111111111';
    v_internal_user_id UUID;
BEGIN
    -- ۱. بررسی یا ایجاد رکورد کاربر در بای‌مانی
    SELECT "Id" INTO v_internal_user_id
    FROM "Users"
    WHERE "ExternalUserId" = v_strapi_document_id;

    IF v_internal_user_id IS NULL THEN
        v_internal_user_id := gen_random_uuid();
        INSERT INTO "Users" (
            "Id",
            "ExternalUserId",
            "IsActive",
            "CreatedAtUtc"
        )
        VALUES (
            v_internal_user_id,
            v_strapi_document_id,
            TRUE,
            NOW() AT TIME ZONE 'UTC'
        );
        RAISE NOTICE 'کاربر جدید با شناسه بای‌مانی % برای شناسه استرپی % ایجاد شد.', v_internal_user_id, v_strapi_document_id;
    ELSE
        RAISE NOTICE 'کاربر موجود با شناسه بای‌مانی % یافت شد.', v_internal_user_id;
    END IF;

    -- ۲. تخصیص نقش مدیر (Admin) به کاربر
    INSERT INTO "UserRoles" ("UserId", "RoleId")
    VALUES (v_internal_user_id, v_admin_role_id)
    ON CONFLICT ("UserId", "RoleId") DO NOTHING;

    RAISE NOTICE 'نقش Admin با موفقیت به کاربر تخصیص داده شد.';
END $$;

-- ۳. بررسی و تأیید نهایی دسترسی‌ها
SELECT 
    u."Id" AS "InternalUserId",
    u."ExternalUserId",
    r."Name" AS "RoleName",
    p."Code" AS "PermissionCode",
    p."Description"
FROM "Users" u
JOIN "UserRoles" ur ON u."Id" = ur."UserId"
JOIN "Roles" r ON ur."RoleId" = r."Id"
JOIN "RolePermissions" rp ON r."Id" = rp."RoleId"
JOIN "Permissions" p ON rp."PermissionId" = p."Id"
WHERE r."Name" = 'Admin';
