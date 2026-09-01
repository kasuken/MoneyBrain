using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Transactions.RecurringTransactions;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.BackgroundServices;

/// <summary>
/// Background service that periodically checks for and generates due recurring transactions.
/// Runs once per hour to keep transactions up-to-date.
/// </summary>
public class RecurringTransactionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringTransactionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public RecurringTransactionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RecurringTransactionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recurring Transaction Background Service started");

        // Run immediately on startup, then every hour
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateRecurringTransactionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recurring transactions");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task GenerateRecurringTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();

        // Get all users with recurring transactions
        var usersWithRecurringTransactions = await context.Transactions
            .Where(t => t.IsRecurring)
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (usersWithRecurringTransactions.Count == 0)
        {
            _logger.LogDebug("No users with recurring transactions found");
            return;
        }

        var totalGenerated = 0;
        // DateTime.UtcNow.Date used as "today" reference date for the look-ahead window.
        var upToDate = DateTime.UtcNow.Date.AddDays(30); // Generate up to 30 days in advance

        foreach (var userId in usersWithRecurringTransactions)
        {
            try
            {
                var generated = await recurringService.GenerateDueRecurringTransactionsAsync(
                    userId,
                    upToDate,
                    cancellationToken);

                totalGenerated += generated;

                if (generated > 0)
                {
                    _logger.LogInformation(
                        "Generated {Count} recurring transactions for user {UserId}",
                        generated,
                        userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating recurring transactions for user {UserId}",
                    userId);
            }
        }

        if (totalGenerated > 0)
        {
            _logger.LogInformation(
                "Total recurring transactions generated: {Count}",
                totalGenerated);
        }
    }
}
