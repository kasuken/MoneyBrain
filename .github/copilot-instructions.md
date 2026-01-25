---
applyTo: '**'
---

# Copilot instructions — MoneyBrain

## Product context (PRD summary)
MoneyBrain is a lightweight, self-hosted personal finance system focused on correctness, predictability, and data ownership.

**Core principles**
- Self-hosted by default; no hidden telemetry or external data transmission.
- Single-user or small household.
- Correctness over cleverness; deterministic behavior.
- Minimal UI; data-first; fast on large datasets.

**Explicit non-goals (v1)**
- No bank sync/PSD2 integrations.
- No invoicing, payroll, accounts receivable.
- No tax filing/optimization.
- No AI-generated financial advice.
- No multi-entity accounting.

## Tech stack
- .NET 10
- Blazor (.NET 10)
- MudBlazor UI components
- C# (best practices, idiomatic patterns, nullable reference types)
- EF Core with SQLite by default (encrypted DB optional later)
- ASP.NET Core Identity for single-user authentication

## Architectural guidance
- Prefer a clean separation of concerns:
  - UI (Blazor + MudBlazor components)
  - Application layer (use-cases, orchestration, validation)
  - Domain model (entities/value objects + invariants)
  - Infrastructure (EF Core, Identity, file/CSV, clock, storage)
- Enforce business rules in the domain/application layer (not only in the UI).
- Keep behavior deterministic (especially rules engine and imports).
- Avoid over-engineering: ship v1 features with simple, clear code.

## Domain model (high-level)
Model features around these concepts:
- **Account**: asset/liability, opening balance, optional group, adjustments with audit trail.
- **Transaction**: date, amount, account, payee, category, memo, tags, status (pending/posted), cleared, reconciled.
- **Transfer**: first-class entity linking exactly two ledger entries (debit/credit) and excluded from budgets/reports.
- **Split transaction**: a transaction can have multiple split lines; sum(splits) == transaction amount.
- **CategoryGroup / Category**: category belongs to exactly one group; support rename/merge and preserve history.
- **Rule**: deterministic matching and actions; ordered priority; preview capability; never mutate reconciled transactions.
- **Budget (monthly envelope)**: planned amounts per category per month; remaining = planned − activity; rollover optional.
- **Reconciliation**: statement-based per account; reconciliation periods; lock reconciled transactions.
- **Reporting**: cashflow, category spending, budget vs actual, net worth; export to CSV.

## Invariants to preserve (acceptance criteria)
Treat these as must-not-break rules:
- Every transaction belongs to exactly one account.
- Account balance is always: opening balance + all applicable entries (+ explicit adjustments).
- Transfers always affect exactly two accounts and never count as income/expense.
- Split totals must equal the transaction amount.
- Only posted transactions affect budgets.
- Reconciled transactions are immutable (no edits, including bulk edits).
- Rules engine never auto-modifies reconciled transactions; execution is deterministic.
- Search/filter must be fast on large datasets (use pagination/virtualization and proper indexing).

## Blazor + MudBlazor UI conventions
- Prefer reusable Blazor components when patterns repeat (selectors, tables, filters, dialogs, forms).
- Use MudBlazor controls for consistent UX (MudTable/MudDataGrid, MudDialog, MudForm).
- Favor keyboard-friendly workflows, inline editing where safe, and minimal click paths.
- For large datasets:
  - Use server-side paging/filtering/sorting.
  - Prefer virtualization where appropriate.
  - Avoid loading everything into memory.
- Keep UI “data-first”: clear tables, filters, and predictable dialogs.

## Data access & performance
- Prefer read models/queries that project only needed columns (avoid heavy entity graphs).
- Avoid N+1 queries; use explicit includes only when needed.
- Add indexes for common filters (date, account, payee, category, reconciled/cleared flags).
- Use transactions for multi-step writes (e.g., creating transfers with linked entries).
- Consider concurrency handling for edits (optimistic concurrency tokens where feasible).

## Import/export (CSV)
- CSV import must support column mapping and preview before applying.
- All transformations (rules, categorization) should be previewable and repeatable.
- Never auto-modify reconciled transactions during import.
- Export should match on-screen values exactly.

## Security & privacy
- Do not add outbound network calls by default.
- No telemetry.
- Keep secrets/config via environment-based configuration.
- Prefer least-privilege, secure defaults; validate and sanitize all user input.

## Coding standards (C#)
- Use nullable reference types and avoid null-forgiving unless justified.
- Prefer async/await end-to-end for I/O.
- Use DI + options pattern for configuration.
- Keep methods small and intention-revealing; avoid clever abstractions.
- Use structured logging (ILogger) and meaningful error messages.

## What to ask before implementing (when unclear)
If requirements are ambiguous, ask concise questions about:
- Whether a change affects reconciled data.
- How transfers should display and whether they’re excluded from specific reports.
- Budget rollover behavior (off by default unless specified).
- Whether a feature is in-scope for v1 vs explicitly a non-goal.
