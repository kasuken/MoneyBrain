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

## 🛠️ Tech Stack

- **Frontend**: Blazor Server (.NET 10) with MudBlazor UI components
- **Backend**: ASP.NET Core (.NET 10)
- **Database**: PostgreSQL 17 (Docker), SQL Server (optional), SQLite (development)
- **ORM**: Entity Framework Core
- **Caching**: In-memory (default), Redis (optional for distributed scenarios)
- **PWA**: Service Workers, manifest.json, offline-first architecture
- **Containerization**: Docker + Docker Compose

**Why this stack:**
- **Self-hosted**: No external dependencies, full data ownership
- **Deterministic**: Server-side rendering ensures consistent behavior
- **Performance**: Blazor Server with SignalR for real-time updates
- **Reliability**: Mature, well-supported technologies with long-term viability

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

### Advanced PWA Features

MoneyBrain's PWA implementation includes advanced capabilities:

- **Smart caching strategies** - Different cache policies for static assets, API calls, and dynamic content
- **Background sync** - Queue transactions offline, sync when connection returns
- **Update notifications** - Get notified when new versions are available
- **Cache management** - Automatic cleanup and version control
- **Performance optimization** - Precaching critical resources for instant load times

**Caching behavior:**
- Static assets (CSS, JS, icons): cached indefinitely, updated on version change
- API responses: network-first with cache fallback
- Offline fallback: custom offline page when network unavailable

**Managing updates:**
1. Updates download automatically in the background
2. You'll see a notification when a new version is ready
3. Refresh the app to activate the update
4. Old cache is cleared automatically

For technical details and troubleshooting, see [PWA_IMPLEMENTATION.md](PWA_IMPLEMENTATION.md).

### Mobile & Responsive Design

MoneyBrain is fully responsive and optimized for mobile devices:

**Mobile-friendly features:**
- **Touch-optimized UI** - Large tap targets, swipe gestures for common actions
- **Responsive layouts** - Adapts to phone, tablet, and desktop screen sizes
- **Fast load times** - Optimized bundle sizes and lazy loading
- **Offline-first** - Core features work without internet connection
- **Install on any device** - Add to home screen on iOS, Android, desktop

**Device-specific optimizations:**
- **Mobile (< 600px)**: Single-column layouts, bottom navigation, simplified tables
- **Tablet (600-960px)**: Two-column layouts, side navigation, compact tables
- **Desktop (> 960px)**: Multi-column layouts, full navigation, detailed tables

**Touch interactions:**
- Swipe left on transactions to quickly access edit/delete actions
- Pull down to refresh transaction lists
- Long-press for bulk selection mode
- Tap-and-hold on categories to see quick stats

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

### Advanced configuration

#### Redis caching (optional)

Enable Redis for distributed caching in multi-instance deployments:

```yaml
services:
  moneybrain:
    environment:
      - Redis__Enabled=true
      - Redis__ConnectionString=redis:6379
    depends_on:
      - redis
  
  redis:
    image: redis:7-alpine
    container_name: moneybrain-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    restart: unless-stopped
    networks:
      - moneybrain-network

volumes:
  redis-data:
```

**What gets cached:**
- Reference data (categories, accounts, payees)
- Budget summaries and calculations
- Net worth snapshots
- Report aggregations

**Cache invalidation:**
- Automatic on data mutations (category edits, transaction posts)
- Manual flush via admin endpoint (if enabled)

#### Currency configuration

MoneyBrain supports multiple currencies with configurable defaults:

```yaml
services:
  moneybrain:
    environment:
      - Currency__Default=USD
      - Currency__Format=en-US
      - Currency__SupportedCurrencies=USD,EUR,GBP,CAD,AUD
```

**Currency options:**
- `Currency__Default`: Default currency code (ISO 4217)
- `Currency__Format`: Locale for formatting (e.g., `en-US`, `de-DE`, `fr-FR`)
- `Currency__SupportedCurrencies`: Comma-separated list of enabled currencies

**Formatting examples:**
- `en-US`: $1,234.56
- `de-DE`: 1.234,56 €
- `fr-FR`: 1 234,56 €
- `en-GB`: £1,234.56

> [!NOTE]
> Multi-currency accounts with exchange rates are planned for future versions.

## Import sample data

There’s a small sample file at `sample-transactions.csv`.

1. Run the app.
2. Go to **Transactions** → **Import CSV**.
3. Upload `sample-transactions.csv`, map columns if needed, preview, then import.

> [!TIP]
> If categories in the CSV don’t exist yet, create them first (Categories) to get cleaner matches.


## 💡 Tips & Insights

### Financial management tips

- **Track recurring expenses**: Use scheduled transactions for bills, subscriptions, and regular payments
- **Category organization**: Group similar categories (e.g., "Food" → "Groceries", "Dining Out") for better reporting
- **Net worth tracking**: Take monthly snapshots to visualize long-term financial progress
- **Reconciliation routine**: Reconcile accounts monthly against statements to catch errors early
- **Budget realism**: Start conservative with budget amounts, adjust based on actual spending patterns

### Power user features

- **Bulk edits**: Select multiple transactions to update categories, tags, or payees at once
- **Split transactions**: Break a single expense across multiple categories (e.g., shopping trip with groceries + household items)
- **Transfer tracking**: Mark transfers between accounts to prevent double-counting in reports
- **Custom date ranges**: Use flexible date filters in reports for quarterly, yearly, or custom period analysis
- **CSV round-trip**: Export transactions, make bulk edits in Excel, re-import (be careful!)

### Data insights

- **Budget variance**: Compare planned vs. actual spending to identify over/under-budget categories
- **Spending trends**: Use category reports over time to spot seasonal patterns
- **Account balance history**: Track how balances change to identify cash flow issues
- **Cleared vs. pending**: Monitor pending transactions to forecast actual vs. projected balances


## 🔍 Insight Explorer

### Common queries and filters

MoneyBrain's search and filter capabilities enable powerful transaction analysis:

**Filter examples:**
- Recent uncleared: `status:posted cleared:false`
- This month's spending: Posted transactions in current month, grouped by category
- All transfers: `type:transfer` (automatically excluded from budgets)
- Budget variance: Planned vs. actual per category for selected month
- Reconciliation candidates: Uncleared transactions within statement date range

**Reporting capabilities:**
- **Cashflow report**: Income vs. expenses over time
- **Category spending**: Breakdown by category with group subtotals
- **Budget vs. actual**: Compare planned amounts to real spending
- **Net worth**: Assets minus liabilities, with historical trend
- **Account balance history**: Track balance changes for any account

**Export & analysis:**
- All reports exportable to CSV for further analysis
- Transaction exports include all fields (date, payee, category, amount, tags, notes)
- Import column mapping for flexible CSV formats

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