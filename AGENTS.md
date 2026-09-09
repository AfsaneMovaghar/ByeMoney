# AGENTS.md — ByeMoney (Summary)

## Context
Modular Monolith (Clean Arch/DDD), digital wallet "Noor", Strapi auth via JWT (HS256). Full arch details in `ByeMoney_Project_Decisions_Summary.md`.

## Stack
.NET 10, PostgreSQL/Npgsql, VS2026 + Antigravity. JWT validation with shared secret.

## Language
Explanations/plans/comments → **Persian**. Code, identifiers, commit msgs → **English**.

## Arch Rules
- Each layer has `DependencyInjection.cs`; `Program.cs` only calls them.
- Packages installed per-layer.
- App layer **never** references Infrastructure.
- Sensitive claims only from JWT, never from request body.
- Repositories only for custom queries.

## Coding
- `DateTime.UtcNow` always (Npgsql rejects non-UTC).
- `ValidateAsync` in ValidationBehavior (never sync — avoids `AsyncValidatorInvokedSynchronouslyException`).
- FluentValidation: `CascadeMode.Stop` (sync before async checks).
- Uniqueness/existence checks → Validator with `MustAsync`, placed after cheaper rules.
- `.resx` via VS designer.

## Handlers
- SRP; extract private methods if logic grows.
- `Handle()` = high-level steps only.
- Shared logic → Service/Helper class.
- Private methods = one clear responsibility.

## Existence Checks (Detail)
- Yes/No existence validation (result not reused) → **Validator** with `MustAsync`.
- If Handler loads & acts on record → fetch once in Handler (avoid duplicate round-trips).

## Scope Discipline
Never add for "future needs." Smallest thing that satisfies current use case.

## Testing
Auto-tests **only** for financial logic (Ledger, Wallet, TopUp, Purchase). Others → Swagger manual. If scope ambiguous → ask before deciding.

## Migrations
Any persistence change **must** include EF Core migration. Task incomplete without it.

## Workflow
- Propose branch name first (`feature/xxx`).
- Plan Mode: explore → plan (files, classes, validation) → approval → execute.

## Localization
Never hard-code user-facing text. Use `.resx` via generated Resource class.
The text of the messages should be in Persian.

## Project Architecture and Integration Contracts

Before implementing or modifying any code related to TarhElahi / Strapi,
you MUST read:

- `docs/integration/ByeMoney-Strapi-Integration.md`
- `docs/architecture/ByeMoney-Decisions.md`

Rules:

1. `ByeMoney-Strapi-Integration.md` is the authoritative integration contract.
2. Do not guess Strapi fields, DTOs, identifiers, API responses, or authentication behavior.
3. If the implementation requires a field or behavior that is not defined in the integration contract, stop and explicitly report the missing contract instead of inventing one.
4. Do not use the frontend implementation as the source of truth for Strapi contracts.
5. Do not introduce Strapi-specific concepts into the ByeMoney Domain layer.
6. For conflicting information:
   - Integration contract wins for API/DTO/integration behavior.
   - ByeMoney-Decisions.md wins for architecture and business boundaries.
7. Any proposed DTO must map explicitly to fields defined in the integration contract.