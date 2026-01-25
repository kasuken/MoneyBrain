# Product Requirements Document (PRD)

## Product name

**MoneyBrain**

**Tagline**
*Your personal finance system, under your control.*

---

## 1. Problem statement

Personal finance tools fall into two broken categories:

* **Spreadsheets**: flexible but fragile, manual, and error-prone.
* **SaaS finance apps**: opaque, subscription-based, privacy-invasive, and often over-scoped.

Users who care about **data ownership, correctness, and long-term clarity** lack a lightweight, self-hosted solution that combines budgeting and proper bookkeeping without turning into accounting software.

---

## 2. Product vision

MoneyBrain is a **lightweight, self-hosted personal finance system** that helps users:

* Track and understand where their money goes.
* Plan spending through simple, disciplined budgeting.
* Maintain correct financial records through proper bookkeeping.
* Retain full ownership and control of their financial data.

MoneyBrain is a **tool**, not a financial advisor.

---

## 3. Target users

### Primary user

* Privacy-conscious individuals.
* Technically comfortable users.
* Self-hosters (Docker, homelab, VPS).
* Users currently relying on spreadsheets or abandoned finance apps.

### Secondary user

* Freelancers managing personal finances separately from business tools.
* Small households sharing finances informally.

### Explicit non-target

* Enterprises.
* Accountants managing multiple clients.
* Users expecting automated tax filing or bank syncing.

---

## 4. Non-goals (explicit)

MoneyBrain **will not** include:

* Bank syncing / PSD2 integrations (v1).
* Invoicing or accounts receivable.
* Payroll.
* Tax filing or tax optimization.
* AI-generated financial advice.
* Multi-entity accounting.
* Subscription-based SaaS features.

---

## 5. Core principles

* **Self-hosted by default**
* **Single-user or small household**
* **Correctness over cleverness**
* **Predictable behavior**
* **Minimal UI, data-first**

---

## 6. Feature set (v1)

### 6.1 Account Management

#### Description

Users can define and manage financial accounts that represent where money lives or is owed.

#### Features

* Create asset accounts (bank, cash, savings).
* Create liability accounts (credit cards, loans).
* Define opening balances.
* Group accounts logically (optional).
* Track account balances over time.
* Perform explicit balance adjustments with audit trail.

#### Acceptance criteria

* Every transaction belongs to exactly one account.
* Account balance always equals opening balance + transactions.
* Adjustments are explicit and traceable.

---

### 6.2 Transaction Management

#### Description

Transactions are the core unit of MoneyBrain. They represent every financial movement.

#### Features

* Manual transaction creation.
* CSV import with column mapping.
* Transaction fields:

  * Date
  * Amount
  * Account
  * Payee
  * Category
  * Memo
  * Tags (optional)
* Search and filter by date, payee, category, amount.
* Bulk edit transactions.
* Pending vs posted transactions.
* Cleared flag.
* Reconciled flag.

#### Acceptance criteria

* Transactions are immutable once reconciled.
* Bulk edits never affect reconciled data.
* Search must be fast on large datasets.

---

### 6.3 Transfers

#### Description

Transfers represent money moving between two accounts without affecting income or expenses.

#### Features

* First-class transfer entity.
* Linked debit and credit entries.
* Transfers excluded from budgets and reports.

#### Acceptance criteria

* A transfer always affects exactly two accounts.
* Transfers never appear as spending or income.

---

### 6.4 Split Transactions

#### Description

Single transactions can be split across multiple categories.

#### Features

* Split a transaction into multiple category lines.
* Each split line has its own amount and category.
* Total split must equal transaction amount.

#### Acceptance criteria

* Budget calculations use split values.
* Reports correctly aggregate split data.

---

### 6.5 Categorization

#### Description

Categories define how money is classified and budgeted.

#### Features

* Category groups (e.g. Housing, Food).
* Categories belong to exactly one group.
* Rename and merge categories.
* Category usage history.

#### Acceptance criteria

* Deleting a category requires reassignment.
* Category history remains intact after renaming.

---

### 6.6 Rules Engine

#### Description

Rules automate transaction cleanup and categorization.

#### Features

* Define rules based on:

  * Payee text
  * Amount
  * Account
* Rule actions:

  * Assign category
  * Rename payee
  * Add tag
* Rule priority ordering.
* Preview rule effects before applying.
* Apply rules during import and manually.

#### Acceptance criteria

* Rules never auto-modify reconciled transactions.
* Rule execution is deterministic.

---

### 6.7 Budgeting

#### Description

MoneyBrain supports **monthly envelope-style budgeting**.

#### Features

* Monthly budgets per category.
* Planned amount per category.
* Remaining budget calculation:

  * Remaining = Planned − Activity
* Overspending indicators.
* Optional rollover of positive balances.
* Copy previous month’s budget.
* Budget notes per category.

#### Acceptance criteria

* Only posted transactions affect budgets.
* Transfers do not affect budgets.
* Budget math is transparent and explainable.

---

### 6.8 Reconciliation & Bookkeeping

#### Description

Reconciliation ensures ledger correctness against external statements.

#### Features

* Statement-based reconciliation per account.
* Reconciliation periods.
* Lock reconciled transactions.
* Audit trail for edits and adjustments.
* Double-entry bookkeeping internally.

#### Acceptance criteria

* Reconciled transactions cannot be modified.
* Balance discrepancies are explicitly flagged.
* Ledger remains internally consistent.

---

### 6.9 Reporting & Insights

#### Description

Reports provide clarity without over-analysis.

#### Features

* Monthly cashflow report.
* Category spending report.
* Budget vs actual report.
* Net worth over time.
* Account balance history.
* Export reports to CSV.

#### Acceptance criteria

* Reports reflect only reconciled or cleared data.
* Exported data matches on-screen values.

---

### 6.10 Data Control & Portability

#### Description

Users fully own their data.

#### Features

* Full data export (CSV).
* Encrypted database support (optional).
* Manual backup and restore.
* No external data transmission by default.

#### Acceptance criteria

* User can exit MoneyBrain without lock-in.
* No hidden telemetry.

---

### 6.11 User Experience

#### Description

Fast, predictable, and distraction-free UI.

#### Features

* Keyboard-friendly navigation.
* Inline editing.
* Global search.
* Dark mode.
* Minimal dashboards.

#### Acceptance criteria

* Common workflows require minimal clicks.
* UI remains usable with large datasets.

---

### 6.12 Deployment & Operations

#### Description

MoneyBrain is designed for simple self-hosting.

#### Features

* Docker-based deployment.
* SQLite by default.
* Environment-based configuration.
* Single-user authentication.

#### Acceptance criteria

* App runs without external dependencies.
* Setup documented and reproducible.

---

## 7. Success metrics (v1)

* User can onboard and import data in <10 minutes.
* Budget and reconciliation workflows are fully usable.
* No data loss during import/export.
* Application usable offline on local network.

---

## 8. Risks

* Over-scoping budgeting features.
* Users expecting SaaS-level automation.
* CSV import edge cases.

Mitigation: strict non-goals and explicit documentation.

---
