# ByeMoney ↔ TarhElahi Strapi Integration

## 1. Purpose

This document defines the integration contract between **ByeMoney** and **TarhElahi / Strapi**.

The two systems remain independent:

- **TarhElahi / Strapi** owns user identity, educational catalog, Rial prices, publication state, educational access, and legacy Rial sales.
- **ByeMoney** owns Noor, Wallet, Ledger, TopUp, Rial→Noor conversion, Purchase/Deal, financial snapshots, and unified reporting.

No system may directly access or modify the other system's database.

---

## 2. Ownership Boundaries

### Strapi owns

- User authentication and external identity
- Course
- CourseChapter
- Product
- Rial catalog price
- Published / available state
- Educational entitlement
- Legacy Rial sales

### ByeMoney owns

- Internal ByeMoney User
- Noor
- Wallet
- Ledger
- TopUp
- Rial → Noor conversion
- Purchase / Deal
- Financial purchase snapshot
- New financial reporting

Important:

```text
Strapi must contain no Noor balance or Noor transaction logic.
Frontend prices are never authoritative for purchase.
```

---

## 3. User Identity

ByeMoney keeps its own internal User and stores the TarhElahi identifier only as an external identity.

Canonical mapping:

```text
ExternalUserId = Strapi User documentId
Phone = attribute
```

`documentId` was verified in the current Strapi database and is populated for existing users.

ByeMoney domain logic must use its own internal `UserId`.

Do not use as permanent integration identity:

```text
numeric database id
phone number
email
username
```

---

## 4. Course Identity

Canonical Course identifier:

```text
ExternalId = Strapi Course documentId
```

Do not use:

```text
numeric database id
slug
title
display order
```

as the integration or financial identity.

---

## 5. Service-to-Service Authentication

Integration endpoints do not use the end-user Bearer JWT.

Required header:

```text
X-Service-Key
```

Environment variable:

```text
BYEMONEY_SERVICE_KEY
```

Requirements:

- `auth: false` on integration routes
- route protected by `global::is-service-authenticated`
- cryptographically secure key, at least 32 random bytes
- no hardcoded production secrets
- production secret stored in environment/secret configuration
- no key/header logging
- missing or invalid credential → `401`
- constant-time comparison using SHA-256 + `crypto.timingSafeEqual`

Canonical policy:

```text
src/policies/is-service-authenticated.js
```

No duplicate global authentication middleware is required.

Current key rotation is standard manual environment rotation, not zero-downtime rotation.

---

## 6. Versioned Integration API

Base namespace:

```text
/api/integrations/byemoney/v1/
```

Current implemented endpoints:

```text
GET /api/integrations/byemoney/v1/users/:externalUserId
GET /api/integrations/byemoney/v1/courses/:externalId
```

Numeric database IDs are not accepted as fallback identifiers.

---

## 7. User Read Contract

Example:

```json
{
  "externalUserId": "...",
  "phoneNumber": "...",
  "email": "...",
  "firstName": "...",
  "lastName": "...",
  "confirmed": true,
  "blocked": false,
  "isMobileVerified": true,
  "createdAt": "...",
  "updatedAt": "..."
}
```

The response is whitelist-only.

Sensitive fields must never be returned, including:

```text
password
resetPasswordToken
confirmationToken
otpCode
otpExpiresAt
cartData
```

Missing user → `404`.

---

## 8. Course Catalog Contract

Example:

```json
{
  "source": "tarh_elahi",
  "type": "course",
  "externalId": "...",
  "parentExternalId": null,
  "title": "...",
  "slug": "...",
  "priceRial": 2500000,
  "published": true,
  "available": true,
  "updatedAt": "..."
}
```

Rules:

```text
priceRial = authoritative price from Strapi
Rial → Noor conversion = ByeMoney responsibility
```

The frontend must not provide the trusted purchase price.

Current Course schema has no separate purchasability flag, therefore currently:

```text
published = publishedAt != null
available = published
```

If Strapi later adds a real purchasability rule, the external contract should keep the same `available` field while its internal calculation changes.

Missing course → `404`.

---

## 9. Purchase Flow

Target flow:

```text
User selects item
        ↓
ByeMoney receives ExternalItemId
        ↓
ByeMoney reads authoritative item from Strapi
        ↓
ByeMoney calculates Noor price
        ↓
Check Ledger balance
        ↓
Create Purchase Snapshot
        ↓
Commit Ledger + Deal
        ↓
Outbox Event
        ↓
Strapi Purchase Confirmation API
        ↓
Grant educational entitlement
```

Strapi must only receive the purchase result after the ByeMoney financial transaction succeeds.

Do not create a normal pending Strapi Order before charging Noor.

---

## 10. Purchase Snapshot

ByeMoney should persist at least:

```text
ProductSource
ExternalItemId
ExternalParentId
ItemType
Title
PriceInRialAtPurchaseTime
ConversionRateAtPurchaseTime
PriceInNoorAtPurchaseTime
PurchasedAt
```

Later Strapi changes must not alter historical financial truth.

---

## 11. Purchase Confirmation

Planned endpoint:

```text
POST /api/integrations/byemoney/v1/purchases/confirm
```

Expected concepts:

```text
eventId
dealId
buyerExternalUserId
item.type
item.externalId
item.parentExternalId
snapshot
purchasedAt
```

Idempotency is mandatory.

Recommended uniqueness:

```text
eventId = UNIQUE
dealId = UNIQUE
```

Duplicate completed events should return success/no-op rather than create duplicate access.

---

## 12. Entitlement

ByeMoney sends domain-level commands only.

Examples:

```text
Grant Course X to User Y
Grant Chapter X to User Y
```

ByeMoney must not know or modify Strapi's internal entitlement relations or JSON structures.

If the user already owns the item:

```text
success
no-op
```

---

## 13. CourseChapter

If a CourseChapter can be purchased independently, it must have a stable integration identifier.

Recommended:

```text
integrationId: UUID
```

Do not use:

```text
component numeric id
array index
display order
slug
```

Chapter purchase contract must contain:

```text
type = course_chapter
externalId = stable chapter integrationId
parentExternalId = course documentId
```

---

## 14. Product

Products owned by TarhElahi remain in Strapi.

Products/services created by ByeMoney users belong to ByeMoney.

For Strapi Products, a stable external identifier and catalog DTO must be defined before purchase integration.

Physical-product inventory is outside the initial scope unless explicitly implemented.

---

## 15. Reliability

Financial success must not depend on Strapi being online at the same moment.

Expected behavior:

```text
Ledger = Successful
Entitlement = Pending
```

Then retry through Outbox until:

```text
Entitlement = Completed
```

Do not rollback a successful Ledger transaction because Strapi is temporarily unavailable.

---

## 16. Reconciliation

Planned endpoint:

```text
GET /api/integrations/byemoney/v1/purchases/:dealId
```

Purpose:

- verify entitlement status
- recover from retries/failures
- support operational reconciliation

Use `DealId` and `CorrelationId` in logs across both systems.

---

## 17. Legacy Sales

Legacy Rial sales remain owned by TarhElahi.

They:

- are used only for reporting
- do not create Noor Ledger entries
- must be separated by an exact Go-Live cutoff timestamp

Planned secure endpoint:

```text
GET /api/integrations/byemoney/v1/legacy-sales
```

with pagination and date/update filters.

---

## 18. Explicitly Forbidden

```text
ByeMoney → direct Strapi DB
Strapi → direct ByeMoney DB
Strapi → modify Noor balance
Strapi → calculate Noor
Frontend → trusted purchase price
ByeMoney → create pending normal Strapi Order
Use slug as financial external ID
Use numeric DB id as integration identity
Use phone as permanent foreign key
Use chapter array index as external ID
Convert legacy Rial sales into Noor Ledger entries
Move card-to-card payment into ByeMoney
```

---

## 19. Current Completed Scope

The Strapi read-side foundation is implemented and tested:

```text
✓ Service-to-service authentication
✓ Versioned Integration API
✓ Stable User external identity
✓ Stable Course external identity
✓ Strict User DTO
✓ Authoritative Course DTO
✓ Authoritative Rial price
✓ published / available
✓ Numeric ID rejection
✓ Sensitive field exclusion
✓ 404 handling
✓ Automated verification
✓ Documentation
```

---

## 20. Next Work

Recommended order:

```text
1. Stable CourseChapter integrationId
2. Product stable ExternalId
3. Chapter/Product catalog contracts
4. Purchase Confirmation API
5. eventId + dealId idempotency
6. Grant Course / Chapter
7. Integration event/audit record
8. Purchase Status / Reconciliation
9. Outbox + Retry
10. Legacy Sales API
```

---

## 21. Final Rule

```text
TarhElahi tells ByeMoney:
"What is being sold, what its Rial price is, and who the user is."

ByeMoney decides:
"How much Noor is required, whether payment succeeds,
and records the financial truth."

ByeMoney tells TarhElahi:
"This purchase is final."

TarhElahi decides:
"How access to the purchased item is granted."
```
