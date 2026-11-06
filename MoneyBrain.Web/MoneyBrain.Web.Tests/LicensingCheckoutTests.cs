using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoneyBrain.Web.Application.Licensing;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Tests;

[TestClass]
public sealed class LicensingCheckoutTests
{
    [TestMethod]
    public async Task CreateCheckoutSessionAsync_RejectsUnconfiguredPriceId()
    {
        var service = new LicenseService(
            new UnexpectedDbContextFactory(),
            Options.Create(new StripeSettings
            {
                SecretKey = "test-key",
                CloudMonthlyPriceId = "price_cloud_monthly",
                CloudAnnualPriceId = "price_cloud_annual"
            }),
            Options.Create(new LicensingSettings()),
            NullLogger<LicenseService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.CreateCheckoutSessionAsync(
                "user-id",
                "user@example.com",
                "price_unconfigured",
                "https://example.com/success",
                "https://example.com/cancel"));
    }

    private sealed class UnexpectedDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            throw new InvalidOperationException("The database should not be accessed for an unconfigured price.");

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The database should not be accessed for an unconfigured price.");
    }
}
