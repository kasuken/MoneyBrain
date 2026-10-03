# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
- The Docker image health check called `curl`, which the .NET runtime image does not include, so self-hosted containers always reported unhealthy. It now checks `/health/live` without extra packages and allows 60 seconds for startup migrations.

- README now documents the actual SQL Server setup (Docker, configuration, backups) instead of PostgreSQL/SQLite, the correct clone URL, and the real `CacheSettings` keys.

### Removed
- `docs/archive/` (outdated implementation status reports).

No versions have been released yet. Earlier changes are available in the git history.
