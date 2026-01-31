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
- **Progressive Web App (PWA)**: install on any device, works offline, app-like experience.

> [!IMPORTANT]
> No bank sync / PSD2 integrations (by design, v1). MoneyBrain is a tool — not a financial advisor.

## 📱 Progressive Web App

MoneyBrain is a full-featured PWA with:

- **🚀 Installable** - Add to home screen on mobile, tablet, or desktop
- **📴 Offline support** - Works without internet connection
- **⚡ Fast & cached** - Smart caching for instant loading
- **🔄 Auto-updates** - Seamless version updates
- **🎨 Native feel** - Runs like a native app in standalone mode

### Installing MoneyBrain

**Desktop (Chrome/Edge):**
- Click the install icon in the address bar
- Or use browser menu → "Install MoneyBrain"

**iOS (Safari):**
- Tap Share button → "Add to Home Screen"

**Android (Chrome):**
- Tap menu (⋮) → "Add to Home screen"
- Or use the in-app install prompt

See [PWA_IMPLEMENTATION.md](PWA_IMPLEMENTATION.md) for complete setup guide.

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

## 🐳 Docker (with PostgreSQL)

Run MoneyBrain with PostgreSQL using Docker Compose.

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/) (included with Docker Desktop)

### Quick start

```bash
# Clone the repository
git clone https://github.com/MoneyBrain-App/MoneyBrain.git
cd MoneyBrain

# Build and start the containers
docker compose up -d

# View logs
docker compose logs -f moneybrain
```

The application will be available at **http://localhost:8080**

### Docker Compose services

| Service | Description | Port |
|---------|-------------|------|
| `moneybrain` | The MoneyBrain web application | 8080 |
| `postgres` | PostgreSQL 17 database | 5432 |

### Configuration

Default environment variables in `docker-compose.yml`:

| Variable | Description | Default |
|----------|-------------|---------|
| `DatabaseProvider` | Database type (`PostgreSQL` or `SQLite`) | `PostgreSQL` |
| `ConnectionStrings__DefaultConnection` | Database connection string | PostgreSQL connection |
| `POSTGRES_DB` | PostgreSQL database name | `moneybrain` |
| `POSTGRES_USER` | PostgreSQL username | `moneybrain` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `moneybrain_secret` |

> [!WARNING]
> For production, change the default database password in `docker-compose.yml`.

### Production configuration

For production deployments, create a `docker-compose.override.yml`:

```yaml
services:
  moneybrain:
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=moneybrain;Username=moneybrain;Password=YOUR_SECURE_PASSWORD
  
  postgres:
    environment:
      - POSTGRES_PASSWORD=YOUR_SECURE_PASSWORD
```

### Managing the containers

```bash
# Start containers
docker compose up -d

# Stop containers
docker compose down

# Stop and remove volumes (WARNING: deletes all data)
docker compose down -v

# Rebuild after code changes
docker compose build --no-cache
docker compose up -d

# View application logs
docker compose logs -f moneybrain

# Access PostgreSQL directly
docker compose exec postgres psql -U moneybrain -d moneybrain
```

### Data persistence

PostgreSQL data is persisted in a Docker volume named `postgres-data`. Your data survives container restarts and updates.

To backup your data:

```bash
# Create a database dump
docker compose exec postgres pg_dump -U moneybrain moneybrain > backup.sql

# Restore from backup
cat backup.sql | docker compose exec -T postgres psql -U moneybrain -d moneybrain
```

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