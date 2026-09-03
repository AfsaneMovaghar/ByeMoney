# AGENTS.md — ByeMoney

Persistent rules for the AI agent working on this repository. Read this before proposing any plan or writing any code. Do not ask the user to repeat these — they always apply unless a prompt explicitly overrides one.

## Project context

ByeMoney is a digital wallet / internal-currency ("Noor") exchange system, built as a Modular Monolith with Clean Architecture / DDD principles. It integrates with an external system called "Strapi" (طرح الهی) via JWT (HS256, shared secret) for identity. See `ByeMoney_Project_Decisions_Summary.md` in the repo root for full architectural ground truth (system boundaries, data ownership, Ledger/Wallet principles, phasing). Do not duplicate that document's content — treat it as authoritative background.

## Tech stack

- .NET 10 / `net10.0`, C#
- PostgreSQL via Npgsql
- Visual Studio 2026 (agent runs as the Antigravity extension inside VS 2026)
- Auth: Strapi issues JWTs (HS256, shared secret); ByeMoney.API validates them

## Response language

Always give explanations, plan descriptions, and comments in **Persian**. Code itself (identifiers, comments in code, commit messages) stays in English/standard conventions unless told otherwise.

## Architecture rules

- Each layer (Domain, Application, Infrastructure, API, etc.) has its own `DependencyInjection.cs` extension method (e.g. `AddApplicationServices`, `AddInfrastructureServices`). `Program.cs` only calls these extension methods — never register services directly in `Program.cs`.
- NuGet packages are installed directly in the layer that uses them (e.g. JwtBearer package lives in `ByeMoney.API`), not centralized in one project.
- **Application layer must never reference concrete Infrastructure implementations.**
- Trust boundary: never accept sensitive claims (Phone, Role, UserId, etc.) from the request body — only from validated JWT claims.
- Use entity-specific repositories only when a custom query is actually required — not for "consistency" or hypothetical future use.

## Coding conventions

- Always use `DateTime.UtcNow`, never `DateTime.Now`, for any persisted timestamp (Npgsql/PostgreSQL rejects non-UTC `timestamptz` values at runtime).
- In `ValidationBehavior`, always call `ValidateAsync`, never the synchronous `Validate` — validators may contain `MustAsync` rules, and calling them synchronously throws `AsyncValidatorInvokedSynchronouslyException`.
- FluentValidation global cascade mode is `CascadeMode.Stop` — order rules so cheap synchronous checks run before async/I-O checks.
- Cross-entity uniqueness checks belong in the Validator via `MustAsync`, placed after cheaper rules — never duplicated in the Handler.
- For `.resx` resources, use Visual Studio's built-in resource designer/code generation rather than hand-editing the XML.

## Phase-one scope discipline

Do not introduce infrastructure, abstractions, or generalization that is only justified by "we may need it later." If you're tempted to add something for future-proofing, flag it explicitly as "Not needed right now — [reason]" instead of adding it. Always implement the smallest thing that satisfies the current use case.

## Testing policy

- Write automated tests **only** for financial logic that touches money/balance: Ledger, Wallet, TopUp, Purchase.
- For everything else (Identity, simple CRUD, etc.), do not write automated tests — manual verification via Swagger is sufficient.
- If a task's scope is ambiguous about whether it touches financial logic, ask before deciding on tests.

## Workflow

- At the start of any new feature or vertical slice, propose a Git branch name (e.g. `feature/xxx`) before starting work.
- Follow the standard Plan Mode flow: explore relevant code → produce an implementation plan artifact (files touched, key classes/interfaces, validation approach) → wait for approval/comments → only then execute.
