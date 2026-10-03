# Third-Party Notices

MoneyBrain is licensed under the [AGPL-3.0-only](LICENSE). It uses the third-party components below, each under its own license. All of them are permissive licenses compatible with the AGPL-3.0; none are copyleft.

## NuGet packages (direct dependencies)

| Package | Version | License | Project |
|---|---|---|---|
| Heron.MudCalendar | 3.4.0 | MIT | https://github.com/danheron/Heron.MudCalendar |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 10.0.2 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.2 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.2 | MIT | https://github.com/dotnet/efcore |
| Microsoft.EntityFrameworkCore.Tools | 10.0.2 | MIT | https://github.com/dotnet/efcore |
| MudBlazor | 8.15.0 | MIT | https://github.com/MudBlazor/MudBlazor |
| StackExchange.Redis | 2.8.16 | MIT | https://github.com/StackExchange/StackExchange.Redis |
| Stripe.net | 47.x | Apache-2.0 | https://github.com/stripe/stripe-dotnet |

Transitive dependencies are under their own licenses; run `dotnet list package --include-transitive` for the full list.

## Bundled front-end assets

| Component | License | Project |
|---|---|---|
| Bootstrap 5.3.3 (`wwwroot/lib/bootstrap`) | MIT | https://getbootstrap.com |

## Web fonts (loaded from Google Fonts)

| Font | License |
|---|---|
| Inter | SIL Open Font License 1.1 |
| Roboto | SIL Open Font License 1.1 |

## Docker images

| Image | Used for | License |
|---|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0` | Build stage | MIT (.NET); base OS packages under their own licenses |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime image | MIT (.NET); base OS packages under their own licenses |
| `mcr.microsoft.com/mssql/server:2022-latest` | Database (Docker Compose) | **Proprietary Microsoft license.** Not distributed with MoneyBrain: it is pulled at runtime, and you accept the [SQL Server EULA](https://go.microsoft.com/fwlink/?linkid=857698) (`ACCEPT_EULA=Y`). The free Developer and Express editions have usage limits. |
