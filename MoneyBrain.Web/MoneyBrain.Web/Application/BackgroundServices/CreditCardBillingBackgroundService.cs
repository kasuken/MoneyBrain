using MoneyBrain.Web.Application.CreditCardBilling;

namespace MoneyBrain.Web.Application.BackgroundServices;

/// <summary>
/// Background service that processes credit card billing cycles.
/// Runs daily to check for credit cards with billing due today.
/// </summary>
public class CreditCardBillingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CreditCardBillingBackgroundService> _logger;
    // Run every hour to check, but billing will only process once per month per card
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public CreditCardBillingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CreditCardBillingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Credit Card Billing Background Service started");

        // Run immediately on startup, then every hour
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueBillingCyclesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing credit card billing cycles");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessDueBillingCyclesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<ICreditCardBillingService>();

        _logger.LogDebug("Checking for credit cards due for billing cycle processing");

        var results = await billingService.ProcessAllDueBillingCyclesAsync(cancellationToken);

        if (!results.Any())
        {
            _logger.LogDebug("No credit cards due for billing today");
            return;
        }

        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        foreach (var result in results)
        {
            if (result.Success)
            {
                _logger.LogInformation(
                    "Processed billing cycle for credit card '{AccountName}': " +
                    "{TransactionCount} transactions posted, total {Amount:C}",
                    result.CreditCardAccountName,
                    result.TransactionsPosted,
                    result.TotalBilledAmount);
            }
            else
            {
                _logger.LogError(
                    "Failed to process billing cycle for credit card '{AccountName}': {Error}",
                    result.CreditCardAccountName,
                    result.ErrorMessage);
            }
        }

        _logger.LogInformation(
            "Credit card billing cycle processing complete: {Successful} successful, {Failed} failed",
            successful,
            failed);
    }
}
