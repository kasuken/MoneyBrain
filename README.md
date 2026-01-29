# MoneyBrain

*Your personal finance system, under your control.*

> [!NOTE]
> MoneyBrain is under active development. The focus is on correctness, predictable behavior, and data ownership.

MoneyBrain is a lightweight, self-hosted personal finance app for tracking accounts and transactions, running a monthly envelope budget, reconciling against statements, and generating practical reports — without bank sync, telemetry, or “financial advice”.

## What you can do

- **Track accounts & balances**: assets and liabilities, opening balances, manual adjustments, and balance history.
- **Manage transactions**: create/edit, search & filter, bulk edits (without touching reconciled data), and posted vs pending.
- **Transfers (between accounts)**: move money without counting it as income/expense.
- **Split transactions**: allocate a single transaction across multiple categories.
- **Categories & groups**: organize spending, rename/merge categories, and keep history intact.
- **Envelope-style budgeting**: plan monthly amounts per category and compare plan vs activity.
- **Reconciliation**: reconcile an account against a statement and lock reconciled transactions.
- **Reports**: cashflow, category spending, budget vs actual, net worth, and account balance history.
- **CSV import & export**: import transactions with column mapping + preview, export reports to CSV.
- **Recurring transactions**: generate upcoming transactions automatically.

> [!IMPORTANT]
> No bank sync / PSD2 integrations (by design, v1). MoneyBrain is a tool — not a financial advisor.

## Quickstart (local)

**Prerequisite:** .NET 10 SDK.

```bash
dotnet restore MoneyBrain.Web/MoneyBrain.Web.sln
dotnet run --project MoneyBrain.Web/MoneyBrain.Web/MoneyBrain.Web.csproj
```

Then open:

- https://localhost:7123
- http://localhost:5103

On first run, register the initial user, then start by creating an account and adding/importing transactions.

## Import sample data

There’s a small sample file at `sample-transactions.csv`.

1. Run the app.
2. Go to **Transactions** → **Import CSV**.
3. Upload `sample-transactions.csv`, map columns if needed, preview, then import.

> [!TIP]
> If categories in the CSV don’t exist yet, create them first (Categories) to get cleaner matches.

## Data ownership & backups

> [!NOTE]
> MoneyBrain is intended to be self-hosted. Your data stays in your database.

- Default database is SQLite and (by default) uses `MoneyBrain.Web/MoneyBrain.Web/Data/app.db`.
- Back up the database file regularly (especially before bulk imports).

## Project scope (v1)

- In scope: budgeting, reconciliation, reporting, CSV import/export, recurring transactions.
- Out of scope: bank sync, invoicing, payroll, tax filing, multi-entity accounting.

## What’s next

MoneyBrain is evolving toward the PRD in `.github/prd.instructions.md`. Some areas are planned but may not be fully implemented yet (for example: a full rules engine with preview).