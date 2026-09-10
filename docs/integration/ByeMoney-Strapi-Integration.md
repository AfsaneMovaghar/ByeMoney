# ByeMoney ↔ TarhElahi / Strapi Integration

## 1. Purpose

This document defines the integration contract between **ByeMoney** and **TarhElahi / Strapi**.

The two systems remain independent:

- **TarhElahi / Strapi** owns user identity, educational catalog, Rial prices, publication state, educational access, and legacy Rial sales.
- **ByeMoney** owns Noor, Wallet, Ledger, TopUp, Rial → Noor conversion, Purchase/Deal, financial snapshots, and unified financial reporting.

> Neither system may directly access or modify the other system's database.

---

## 2. Ownership Boundaries

### TarhElahi / Strapi owns

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

### Core rules

- Strapi must contain **no Noor balance or Noor transaction logic**.
- Frontend prices are **never authoritative** for purchase.
- ByeMoney must use its own internal `UserId` in domain logic.

---

## 3. User Identity

ByeMoney keeps its own internal User and stores the TarhElahi identifier only as an external identity.

Canonical mapping:

```text
ExternalUserId = Strapi User documentId
Phone = attribute
```

`documentId` has been verified in the current Strapi database and is populated for existing users.

Do **not** use these as permanent integration identities:

```text
numeric database id
phone number
email
username
```

### User activity rule

ByeMoney calculates user activity during synchronization:

```text
IsActive = confirmed && !blocked
```

This value is derived in ByeMoney, not stored or calculated by Strapi for the integration contract.

---

## 4. Course Identity

Canonical Course identifier:

```text
ExternalId = Strapi Course documentId
```

Do **not** use the following as integration or financial identity:

```text
numeric database id
slug
title
display order
```

---

## 5. Service-to-Service Authentication

Integration endpoints do **not** use the end-user Bearer JWT.

Required header:

```text
X-Service-Key
```

Environment variable:

```text
BYEMONEY_SERVICE_KEY
```

Requirements:

- Integration routes use `auth: false`.
- Routes are protected by `global::is-service-authenticated`.
- The key must contain at least 32 cryptographically secure random bytes.
- Production secrets must never be hardcoded, committed, or logged.
- Production secrets must be stored in environment/secret configuration.
- Missing or invalid credentials return `401`.
- Key comparison uses SHA-256 with `crypto.timingSafeEqual`.
- No duplicate global authentication middleware is required.

Canonical policy:

```text
src/policies/is-service-authenticated.js
```

Current key rotation is standard manual environment rotation, not zero-downtime rotation.

---

## 6. Versioned Integration API

Base namespace:

```text
/api/integrations/byemoney/v1/
```

Currently implemented endpoints:

```text
GET /api/integrations/byemoney/v1/users/:externalUserId
GET /api/integrations/byemoney/v1/courses/:externalId
```

Numeric database IDs are not accepted as fallback identifiers.

---

## 7. User Read Contract

Example response:

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

Fields such as the following must never be returned:

```text
password
resetPasswordToken
confirmationToken
otpCode
otpExpiresAt
cartData
username
```

Missing user → `404`.

---

## 8. Course Catalog Contract

Example response:

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

`priceRial` must be handled as a **decimal-compatible value**, not forced into `long`/`int`, because Strapi does not guarantee integer rounding.

The frontend must never provide the trusted purchase price.

The current Course schema has no separate purchasability flag, therefore:

```text
published = publishedAt != null
available = published
```

If Strapi later introduces a real purchasability rule, the external contract should keep the same `available` field while only its internal calculation changes.

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

Rules:

- Strapi receives the purchase result only **after** the ByeMoney financial transaction succeeds.
- Do not create a normal pending Strapi Order before charging Noor.

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

Later changes in Strapi must not alter historical financial truth.

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

If an already-completed event is received again, Strapi should return success/no-op instead of granting duplicate access.

---

## 12. Educational Entitlement

ByeMoney sends **domain-level commands only**.

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

Do **not** use:

```text
component numeric id
array index
display order
slug
```

Chapter purchase contract:

```text
type = course_chapter
externalId = stable chapter integrationId
parentExternalId = course documentId
```

---

## 14. Product Ownership and Identity

- Products owned by TarhElahi remain in Strapi.
- Products/services created by ByeMoney users belong to ByeMoney.
- For Strapi Products, a stable external identifier and catalog DTO must be defined before purchase integration.
- Physical-product inventory is outside the initial scope unless explicitly implemented.

---

## 15. Reliability and Outbox

Financial success must not depend on Strapi being online at the same moment.

Expected temporary state:

```text
Ledger = Successful
Entitlement = Pending
```

The entitlement operation is then retried through the Outbox until:

```text
Entitlement = Completed
```

A successful Ledger transaction must **not** be rolled back merely because Strapi is temporarily unavailable.

---

## 16. Reconciliation

Planned endpoint:

```text
GET /api/integrations/byemoney/v1/purchases/:dealId
```

Purpose:

- Verify entitlement status.
- Recover from retries or failures.
- Support operational reconciliation.

Use `DealId` and `CorrelationId` in logs across both systems.

---

## 17. Legacy Rial Sales

Legacy Rial sales remain owned by TarhElahi.

They:

- are used only for reporting,
- do not create Noor Ledger entries,
- must be separated using an exact Go-Live cutoff timestamp.

Planned secure endpoint:

```text
GET /api/integrations/byemoney/v1/legacy-sales
```

The endpoint should support pagination and date/update filters.

---

## 18. Explicitly Forbidden

```text
ByeMoney → direct Strapi DB access
Strapi → direct ByeMoney DB access
Strapi → modify Noor balance
Strapi → calculate Noor
Frontend → provide trusted purchase price
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

The Strapi read-side integration foundation is implemented and tested:

- [x] Service-to-service authentication
- [x] Versioned Integration API
- [x] Stable User external identity using `documentId`
- [x] Stable Course external identity using `documentId`
- [x] Strict User DTO
- [x] Authoritative Course DTO
- [x] Authoritative Rial price
- [x] `published` / `available`
- [x] Numeric ID rejection
- [x] Sensitive-field exclusion
- [x] `404` handling
- [x] Automated verification
- [x] Documentation

---

## 20. Next Work

Recommended order:

1. Add stable `CourseChapter.integrationId`.
2. Define stable Product `ExternalId`.
3. Define Chapter/Product catalog contracts.
4. Implement Purchase Confirmation API.
5. Add `eventId` + `dealId` idempotency.
6. Implement Grant Course / Chapter.
7. Add integration event/audit record.
8. Implement Purchase Status / Reconciliation.
9. Implement Outbox + Retry.
10. Implement Legacy Sales API.

---

## 21. Final Responsibility Rule

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
