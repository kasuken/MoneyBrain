using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Budgets;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Common.Services;
using MoneyBrain.Web.Application.CreditCardBilling;
using MoneyBrain.Web.Application.Licensing;
using MoneyBrain.Web.Application.Reconciliation;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Application.Reporting.Cashflow;
using MoneyBrain.Web.Application.Reporting.CategorySpending;
using MoneyBrain.Web.Application.Reporting.BudgetComparison;
using MoneyBrain.Web.Application.Reporting.NetWorth;
using MoneyBrain.Web.Application.Reporting.AccountBalanceHistory;
using MoneyBrain.Web.Application.Reporting.CsvExport;
using MoneyBrain.Web.Application.Transactions;
using MoneyBrain.Web.Application.Transactions.CsvImport;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Application.Transactions.RecurringTransactions;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Components;
using MoneyBrain.Web.Components.Account;
using MoneyBrain.Web.Data;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddMudServices();

// Localization
// Note: Do NOT use ResourcesPath since the marker class (SharedResource) is already in 
// the MoneyBrain.Web.Resources namespace - the localizer uses the full namespace to find resources
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "it", "es", "de" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// Controllers for localization
builder.Services.AddControllers();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Cache service
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();

// MoneyBrain application services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ITransactionCsvImportService, TransactionCsvImportService>();
builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<ICashflowService, CashflowService>();
builder.Services.AddScoped<ICategorySpendingService, CategorySpendingService>();
builder.Services.AddScoped<IBudgetComparisonService, BudgetComparisonService>();
builder.Services.AddScoped<INetWorthService, NetWorthService>();
builder.Services.AddScoped<IAccountBalanceHistoryService, AccountBalanceHistoryService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<ICreditCardBillingService, CreditCardBillingService>();
builder.Services.AddScoped<IInsightExplorerService, InsightExplorerService>();

// Mobile detection service
builder.Services.AddScoped<MoneyBrain.Web.Services.IMobileDetectionService, MoneyBrain.Web.Services.MobileDetectionService>();

// Licensing services
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<LicensingSettings>(builder.Configuration.GetSection("Licensing"));
builder.Services.AddScoped<ILicenseService, LicenseService>();

// Background services
builder.Services.AddHostedService<MoneyBrain.Web.Application.BackgroundServices.RecurringTransactionBackgroundService>();
builder.Services.AddHostedService<MoneyBrain.Web.Application.BackgroundServices.CreditCardBillingBackgroundService>();

var app = builder.Build();

// Apply database migrations automatically (for Docker / production)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Only use HTTPS redirection when not running behind a reverse proxy
if (!string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"]) ||
    app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// If the user is already logged in, send them straight to the app dashboard.
// This avoids rendering the (static) marketing home page for authenticated sessions.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("dashboard");
        return;
    }

    await next();
});

app.UseAntiforgery();

app.UseRequestLocalization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Stripe webhook endpoint for subscription lifecycle events
app.MapPost("/api/stripe/webhook", async (HttpContext context, ILicenseService licenseService) =>
{
    var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var signature = context.Request.Headers["Stripe-Signature"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(signature))
    {
        return Results.BadRequest("Missing Stripe-Signature header");
    }

    try
    {
        await licenseService.HandleWebhookAsync(json, signature);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).AllowAnonymous();

app.Run();