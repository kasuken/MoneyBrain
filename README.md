# MoneyBrain

[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue?style=flat-square)](LICENSE)

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
- **Progressive Web App (PWA)**: install on any device for an app-like experience.

## 🛠️ Tech Stack

- **Frontend**: Blazor Server (.NET 10) with MudBlazor UI components
- **Backend**: ASP.NET Core (.NET 10)
- **Database**: SQL Server (SQL Server 2022 in Docker, LocalDB for local development on Windows)
- **ORM**: Entity Framework Core
- **Caching**: In-memory (default), Redis (optional for distributed scenarios)
- **PWA**: Web app manifest with mobile and desktop installation support
- **Containerization**: Docker + Docker Compose

**Why this stack:**
- **Self-hosted**: Runs on your own infrastructure with full data ownership
- **Deterministic**: Server-side rendering ensures consistent behavior
- **Performance**: Blazor Server with SignalR for real-time updates
- **Reliability**: Mature, well-supported technologies with long-term viability

> [!IMPORTANT]
> No bank sync / PSD2 integrations (by design, v1). MoneyBrain is a tool — not a financial advisor.

## 📱 Progressive Web App

MoneyBrain provides a minimal, installation-only PWA:

- **🚀 Installable** - Add to home screen on mobile, tablet, or desktop
- **🎨 Native feel** - Runs like a native app in standalone mode

The PWA does not register a service worker, cache application resources, or
provide offline behavior. An internet connection is required.

### Installing MoneyBrain

**Desktop (Chrome/Edge):**
- Click the install icon in the address bar
- Or use browser menu → "Install MoneyBrain"

**iOS (Safari):**
- Tap Share button → "Add to Home Screen"

**Android (Chrome):**
- Tap menu (⋮) → "Add to Home screen"
- Or use the in-app install prompt

### Mobile & Responsive Design

MoneyBrain is fully responsive and optimized for mobile devices:

**Mobile-friendly features:**
- **Touch-optimized UI** - Large tap targets, swipe gestures for common actions
- **Responsive layouts** - Adapts to phone, tablet, and desktop screen sizes
- **Fast load times** - Optimized bundle sizes and lazy loading
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

**Prerequisites:** .NET 10 SDK and SQL Server. The default connection string in `appsettings.json` uses SQL Server LocalDB (Windows). On macOS or Linux, start a SQL Server container (see below) and point `ConnectionStrings:DefaultConnection` at it with user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=moneybrain;User Id=sa;Password=<your-password>;TrustServerCertificate=True" --project MoneyBrain.Web/MoneyBrain.Web/MoneyBrain.Web.csproj
```

Then run:

```bash
dotnet restore MoneyBrain.Web/MoneyBrain.Web.sln
dotnet run --project MoneyBrain.Web/MoneyBrain.Web/MoneyBrain.Web.csproj
```

Open https://localhost:7123 or http://localhost:5103. Database migrations are applied automatically at startup.

On first run, register the initial user, then start by creating an account and adding/importing transactions.

## 🐳 Docker (with SQL Server)

Run MoneyBrain and SQL Server 2022 with Docker Compose.

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/) (included with Docker Desktop)

### Quick start

```bash
# Clone the repository
git clone https://github.com/kasuken/MoneyBrain.git
cd MoneyBrain

# Create your .env file and set a strong SQL Server password
cp .env.example .env

# Build and start the containers
docker compose up -d

# View logs
docker compose logs -f moneybrain
```

The application will be available at **http://localhost:8080**.

### Docker Compose services

| Service | Description | Port |
|---------|-------------|------|
| `moneybrain` | The MoneyBrain web application | 8080 |
| `sqlserver` | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`) | 1433 |

> [!NOTE]
> The SQL Server image is distributed by Microsoft under its own license terms. `ACCEPT_EULA=Y` in `docker-compose.yml` accepts them. The Developer edition is used by default; check Microsoft's licensing for production use.

### Configuration

Settings come from `.env` and the `environment` section of `docker-compose.yml`:

| Variable | Description | Default |
|----------|-------------|---------|
| `MSSQL_SA_PASSWORD` (`.env`) | SQL Server `sa` password, also used in the app's connection string. Must meet SQL Server complexity rules. | none, required |
| `ConnectionStrings__DefaultConnection` | Database connection string | `Server=sqlserver;Database=moneybrain;User Id=sa;...` |
| `CacheSettings__Provider` | `Memory` or `Redis` | `Memory` |
| `Licensing__Enabled` | Subscription licensing used by the hosted service | `false` |

> [!WARNING]
> Never commit your `.env` file. For production, use a dedicated SQL login instead of `sa`.

### Production configuration

For production deployments, create a `docker-compose.override.yml`, for example to use a dedicated SQL login or an external SQL Server:

```yaml
services:
  moneybrain:
    environment:
      - ConnectionStrings__DefaultConnection=Server=<host>;Database=moneybrain;User Id=<user>;Password=<password>;TrustServerCertificate=True
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

# Open a SQL shell
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d moneybrain
```

### Data persistence

SQL Server data is persisted in the Docker volume `sqlserver-data`, so it survives container restarts and updates.

To back up your data:

```bash
# Create a backup inside the container and copy it out
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "BACKUP DATABASE [moneybrain] TO DISK = N'/var/opt/mssql/data/moneybrain.bak' WITH INIT"
docker compose cp sqlserver:/var/opt/mssql/data/moneybrain.bak ./moneybrain.bak

# Restore from a backup
docker compose cp ./moneybrain.bak sqlserver:/var/opt/mssql/data/moneybrain.bak
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "RESTORE DATABASE [moneybrain] FROM DISK = N'/var/opt/mssql/data/moneybrain.bak' WITH REPLACE"
```

### Advanced configuration

#### Redis caching (optional)

Use Redis for distributed caching in multi-instance deployments:

```yaml
services:
  moneybrain:
    environment:
      - CacheSettings__Provider=Redis
      - CacheSettings__Redis__ConnectionString=redis:6379
    depends_on:
      - redis

  redis:
    image: redis:7-alpine
    container_name: moneybrain-redis
    volumes:
      - redis-data:/data
    restart: unless-stopped
    networks:
      - moneybrain-network

volumes:
  redis-data:
```

Other Redis options (`Database`, `InstanceName`, `SslEnabled`, `ConnectTimeout`, `SyncTimeout`) are under `CacheSettings:Redis` in `appsettings.json`.

#### Currencies

Each account has its own currency, set when you create the account. Multi-currency accounts with exchange rates are planned for future versions.

## Import sample data

There’s a small sample file at `samples/sample-transactions.csv`.

1. Run the app.
2. Go to **Transactions** → **Import CSV**.
3. Upload `samples/sample-transactions.csv`, map columns if needed, preview, then import.

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
> MoneyBrain is intended to be self-hosted. Your data stays in your own SQL Server database.

- Back up the database regularly, especially before bulk imports. See [Data persistence](#data-persistence) for a Docker backup and restore example.

## Project scope (v1)

- In scope: budgeting, reconciliation, reporting, CSV import/export, recurring transactions.
- Out of scope: bank sync, invoicing, payroll, tax filing, multi-entity accounting.

## What’s next

MoneyBrain is evolving toward the PRD in `.github/prd.instructions.md`. Some areas are planned but may not be fully implemented yet (for example: a full rules engine with preview).

## Hosted or self-hosted

MoneyBrain is open source and built to be self-hosted: the Docker Compose setup in this repository runs the app with SQL Server, an in-memory cache and no subscription checks. The code also contains the subscription licensing used by the hosted service (Stripe billing, Redis cache). It is controlled by the `Licensing:Enabled` setting, which is `false` by default and only turned on in `appsettings.Production.json` for the hosted deployment. A self-hosted instance never needs Stripe keys.

## Contributing

Contributions are welcome! Please read the [contributing guidelines](https://github.com/kasuken/.github/blob/main/CONTRIBUTING.md) and the [Code of Conduct](https://github.com/kasuken/.github/blob/main/CODE_OF_CONDUCT.md) before opening a pull request. All contributors must sign the [Contributor License Agreement](https://github.com/kasuken/.github/blob/main/CLA.md); a bot will ask you to on your first pull request.

## Security

Please **do not** report security vulnerabilities in public issues. Use [private vulnerability reporting](https://github.com/kasuken/MoneyBrain/security/advisories/new) instead. See the [Security Policy](https://github.com/kasuken/.github/blob/main/SECURITY.md) for details.

## License

MoneyBrain is licensed under the [GNU Affero General Public License v3.0 only](LICENSE) (`AGPL-3.0-only`). If you run a modified version of MoneyBrain as a network service, the AGPL requires you to make your modified source code available to its users. Set `SourceCodeUrl` in configuration to point the in-app "Source code" link at your repository.

Third-party components and their licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

"MoneyBrain" and the MoneyBrain logo are trademarks of Emanuele Bartolesi and are not licensed under the AGPL. If you publish a modified public instance, please use a different name and logo.
