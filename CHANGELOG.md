# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-11-07

### Added
- MoneyBrain Cloud monthly and annual Stripe checkout, with self-hosted use remaining free and independent of Stripe.
- Pricing and product guidance for MoneyBrain Cloud, Self-Hosted, and the planned Family tier.

### Changed
- Documented that hosted and self-hosted data stays on its respective server, with no cross-deployment sync.
- Configure hosted Stripe monthly and annual Price IDs through deployment settings; checkout accepts only those configured plans.

## [1.1.0] - 2026-11-06

### Added
- Opt-in remote MCP server for external AI clients, with OAuth authorization code/PKCE, explicit read/write consent, and revocable connected-client grants.
- User-scoped finance tools, reports, saved queries, resources and workflow prompts; MCP setup and privacy guidance in `docs/MCP.md`.
- Automated MCP authentication, protocol, data-isolation and financial-rule coverage, run in CI.
- Licensed under AGPL-3.0-only, with a Contributor License Agreement for contributions.
- In-app "Source code" link, configurable through `SourceCodeUrl`.
- Issue forms, CODEOWNERS, Dependabot configuration and third-party notices.
- `/health/live` and `/health/ready` endpoints; every release is smoke tested against `/health/ready`.

### Changed
- Pushing to `main` no longer deploys. Releases use the shared kasuken release workflow (run **Release** with a version bump), which waits for CI, deploys with Azure OIDC instead of a publish profile, smoke tests, and tags the release.
- `appsettings.Production.json` no longer enables licensing, Redis or detailed errors; the hosted service sets those as App Service settings.
- Docker Compose now runs self-hosted defaults: in-memory cache and subscription licensing disabled.
- `agents.md` renamed to `AGENTS.md`; Copilot instructions moved to `.github/copilot-instructions.md`.
- Sample import file moved to `samples/sample-transactions.csv`.

### Fixed
- **Transfers** - Linked transfer entries are validated as one balanced ledger operation. Updating a transfer now updates its ledger atomically, and deleting it removes both sides without leaving ledger records.
- **Reconciliation** - Selections must contain eligible posted transactions through the statement date. Totals update atomically, and only balanced statements can be finalized.
- **Accounts** - Accounts with financial history or references must be deactivated rather than deleted. Account operation logs no longer include financial amounts, names or adjustment descriptions.
- **Transaction writes** - Transaction, split and ledger changes are atomic; normalized split totals must match exactly at cent precision. Bulk category changes update the ledger, and flag changes invalidate affected reports.
- **Recurring transactions** - Generation and schedule advancement commit together, retries avoid duplicate occurrences, and reconciled templates remain unchanged.
- **Category and payee merges** - Merge checks run atomically, reconciled references remain protected, cross-type category merges are rejected, and unused-payee cleanup validates the exact previewed selection.
- **Saved query date filters** - Insight Explorer now uses the correct date type and nullable comparison values instead of silently ignoring those filters.
- The Docker image health check called `curl`, which the .NET runtime image does not include, so self-hosted containers always reported unhealthy. It now checks `/health/live` without extra packages and allows 60 seconds for startup migrations.

- README now documents the actual SQL Server setup (Docker, configuration, backups) instead of PostgreSQL/SQLite, the correct clone URL, and the real `CacheSettings` keys.

### Removed
- `docs/archive/` (outdated implementation status reports).

## [1.0.0] - 2026-10-03

Initial published release. Earlier changes are available in the git history.
