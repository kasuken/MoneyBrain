using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Budgets;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Application.Common.Configuration;
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
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Application.Transactions.RecurringTransactions;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Application.Tips;
using MoneyBrain.Web.Components;
using MoneyBrain.Web.Components.Account;
using MoneyBrain.Web.Data;
using MudBlazor.Services;
using StackExchange.Redis;
using StripeException = Stripe.StripeException;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment() ||
                                 builder.Configuration.GetValue<bool>("DetailedErrors");
    });

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

// AddDbContextFactory with explicit ServiceLifetime.Scoped prevents the factory from
// capturing the scoped options into a singleton lifetime, which would cause issues with
// per-request services. Blazor Server components and application services use this factory
// to create short-lived, per-operation contexts that avoid the "second operation started"
// concurrency error common with long-lived Blazor Server circuits.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
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
var cacheSettings = builder.Configuration.GetSection("CacheSettings").Get<CacheSettings>() ?? new CacheSettings();

if (cacheSettings.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    // Redis cache
    if (cacheSettings.Redis == null || string.IsNullOrWhiteSpace(cacheSettings.Redis.ConnectionString))
    {
        throw new InvalidOperationException("Redis connection string is required when using Redis cache provider.");
    }

    var redisOptions = ConfigurationOptions.Parse(cacheSettings.Redis.ConnectionString);
    redisOptions.ConnectTimeout = cacheSettings.Redis.ConnectTimeout;
    redisOptions.SyncTimeout = cacheSettings.Redis.SyncTimeout;
    redisOptions.Ssl = cacheSettings.Redis.SslEnabled;
    redisOptions.AbortOnConnectFail = false; // Resilient connection

    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            var redis = ConnectionMultiplexer.Connect(redisOptions);
            logger.LogInformation("Redis connection established successfully");
            return redis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Redis");
            throw;
        }
    });

    builder.Services.AddSingleton<ICacheService>(sp =>
    {
        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
        var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
        return new RedisCacheService(redis, logger, cacheSettings.Redis.InstanceName);
    });
}
else
{
    // Memory cache (default)
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
}

// MoneyBrain application services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<IUserDataService, UserDataService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMonthlyBudgetService, MonthlyBudgetService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPayeeService, PayeeService>();
builder.Services.AddScoped<ITransferService, TransferService>();
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

// Tips & Insights services
builder.Services.AddScoped<IEducationalTipService, EducationalTipService>();
builder.Services.AddScoped<ITipPreferenceService, TipPreferenceService>();
builder.Services.AddScoped<ISpendingInsightService, SpendingInsightService>();
builder.Services.AddScoped<IBudgetInsightService, BudgetInsightService>();
builder.Services.AddScoped<INetWorthInsightService, NetWorthInsightService>();
builder.Services.AddScoped<IBehaviorInsightService, BehaviorInsightService>();

// Mobile detection service
builder.Services.AddScoped<MoneyBrain.Web.Services.IMobileDetectionService, MoneyBrain.Web.Services.MobileDetectionService>();

// Currency formatting service
builder.Services.AddScoped<MoneyBrain.Web.Services.ICurrencyFormattingService, MoneyBrain.Web.Services.CurrencyFormattingService>();

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

// Security headers — applied early, before authentication middleware.
// Content-Security-Policy is intentionally omitted: MudBlazor relies on inline
// styles and scripts; a strict CSP would break the UI. Revisit when MudBlazor
// ships nonce/hash support.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

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

// Configure cache-control headers for static assets
// Fingerprinted assets (via @Assets[]) get long-term immutable cache
// Non-fingerprinted assets get short-term revalidation cache
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Check if the asset is fingerprinted (MapStaticAssets adds version parameter)
        if (ctx.Context.Request.Query.ContainsKey("v"))
        {
            // Immutable cache for 1 year for fingerprinted assets
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            // Short cache with revalidation for non-fingerprinted assets (e.g., external fonts)
            ctx.Context.Response.Headers.CacheControl = "public,max-age=3600,must-revalidate";
        }
    }
});

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Stripe webhook endpoint for subscription lifecycle events
app.MapPost("/api/stripe/webhook", async (HttpContext context, ILicenseService licenseService, ILogger<Program> logger) =>
{
    var json = await new StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted);
    var signature = context.Request.Headers["Stripe-Signature"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(signature))
    {
        return Results.BadRequest("Missing Stripe-Signature header");
    }

    try
    {
        await licenseService.HandleWebhookAsync(json, signature, context.RequestAborted);
        return Results.Ok();
    }
    catch (StripeException ex)
    {
        // Stripe signature/payload errors are client errors — don't retry.
        logger.LogError(ex, "Stripe error processing webhook");
        return Results.BadRequest("Webhook processing failed");
    }
    catch (Exception ex)
    {
        // Unexpected internal error — return 500 so Stripe retries the event.
        logger.LogError(ex, "Unexpected error processing Stripe webhook");
        return Results.Problem("Webhook processing failed");
    }
}).AllowAnonymous();

app.Run();