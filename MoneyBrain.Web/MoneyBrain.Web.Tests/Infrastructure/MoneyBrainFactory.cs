using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoneyBrain.Web.Data;
using OpenIddict.Server;
using OpenIddict.Validation;

namespace MoneyBrain.Web.Tests.Infrastructure;

internal sealed class MoneyBrainFactory(bool enabled = true, bool enableWrites = false, bool shortLivedTokens = false,
    bool configureClient = false,
    IReadOnlyDictionary<string, string?>? extraSettings = null)
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    internal TestLogProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddProvider(Logs);
        });
        builder.UseSetting("Database:ApplyMigrations", "false");
        builder.UseSetting("Mcp:Enabled", enabled.ToString());
        builder.UseSetting("Mcp:EnableWrites", enableWrites.ToString());
        builder.UseSetting("Mcp:PublicUrl", "https://localhost");
        // Program reads some switches while building services, before ConfigureAppConfiguration is applied.
        foreach (var pair in extraSettings ?? new Dictionary<string, string?>())
            builder.UseSetting(pair.Key, pair.Value);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:ApplyMigrations"] = "false",
                ["Mcp:Enabled"] = enabled.ToString(),
                ["Mcp:EnableWrites"] = enableWrites.ToString(),
                ["Mcp:PublicUrl"] = "https://localhost",
                ["CacheSettings:Provider"] = "Memory",
                ["Logging:LogLevel:Default"] = "Warning"
            };
            if (configureClient)
            {
                settings["Mcp:Clients:0:ClientId"] = OAuthFlow.ClientId;
                settings["Mcp:Clients:0:DisplayName"] = "Configured integration test client";
                settings["Mcp:Clients:0:RedirectUris:0"] = OAuthFlow.RedirectUri;
                settings["Mcp:Clients:0:AllowWrites"] = enableWrites.ToString();
            }
            if (extraSettings is not null)
                foreach (var pair in extraSettings)
                    settings[pair.Key] = pair.Value;
            configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureServices(services =>
        {
            connection.Open();
            // Provisioning runs before the host starts even when migrations are disabled.
            // Create only this fixture's relational schema without building a second DI container.
            using (var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                       .UseSqlite(connection).Options))
                context.Database.EnsureCreated();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection),
                ServiceLifetime.Scoped);
            if (shortLivedTokens)
            {
                services.PostConfigure<OpenIddictServerOptions>(options =>
                    options.AccessTokenLifetime = TimeSpan.FromSeconds(1));
                services.PostConfigure<OpenIddictValidationOptions>(options =>
                    options.TokenValidationParameters.ClockSkew = TimeSpan.Zero);
            }
            // Scheduled jobs are unrelated to HTTP behavior and must not mutate fixtures.
            foreach (var descriptor in services.Where(descriptor =>
                         descriptor.ServiceType == typeof(IHostedService) &&
                         descriptor.ImplementationType?.Namespace ==
                         "MoneyBrain.Web.Application.BackgroundServices").ToArray())
                services.Remove(descriptor);
        });
    }

    internal HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    internal async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}
