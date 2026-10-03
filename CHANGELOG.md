# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Licensed under AGPL-3.0-only, with a Contributor License Agreement for contributions.
- In-app "Source code" link, configurable through `SourceCodeUrl`.
- Issue forms, CODEOWNERS, Dependabot configuration and third-party notices.

### Changed
- Docker Compose now runs self-hosted defaults: in-memory cache and subscription licensing disabled.
- `agents.md` renamed to `AGENTS.md`; Copilot instructions moved to `.github/copilot-instructions.md`.
- Sample import file moved to `samples/sample-transactions.csv`.

### Fixed

- README now documents the actual SQL Server setup (Docker, configuration, backups) instead of PostgreSQL/SQLite, the correct clone URL, and the real `CacheSettings` keys.

### Removed
- `docs/archive/` (outdated implementation status reports).

No versions have been released yet. Earlier changes are available in the git history.
