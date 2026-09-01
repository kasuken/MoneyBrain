using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Settings;

/// <summary>
/// Handles destructive user-data operations: erasing all data and loading demo data.
/// </summary>
public class UserDataService : IUserDataService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ICacheService _cacheService;

    public UserDataService(IDbContextFactory<ApplicationDbContext> contextFactory, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task EraseAllUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Delete in order to respect foreign key constraints
        // Use ExecuteDeleteAsync for efficient bulk deletion

        // 1. Delete ledger entries (references transactions and accounts)
        await context.LedgerEntries
            .Where(le => le.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 2. Delete transaction splits (references transactions)
        var transactionIds = await context.Transactions
            .Where(t => t.UserId == userId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (transactionIds.Count > 0)
        {
            await context.TransactionSplits
                .Where(ts => transactionIds.Contains(ts.TransactionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // 3. Delete transactions
        await context.Transactions
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 4. Delete reconciliations (references accounts)
        await context.Reconciliations
            .Where(r => r.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 5. Delete account-related data (snapshots, adjustments)
        var accountIds = await context.Accounts
            .Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (accountIds.Count > 0)
        {
            await context.AccountBalanceSnapshots
                .Where(s => accountIds.Contains(s.AccountId))
                .ExecuteDeleteAsync(cancellationToken);

            await context.ManualBalanceAdjustments
                .Where(a => accountIds.Contains(a.AccountId))
                .ExecuteDeleteAsync(cancellationToken);

            await context.OpeningBalanceAdjustments
                .Where(a => accountIds.Contains(a.AccountId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // 6. Delete accounts
        await context.Accounts
            .Where(a => a.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 7. Delete budget categories (references budgets and categories)
        var budgetIds = await context.Budgets
            .Where(b => b.UserId == userId)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        if (budgetIds.Count > 0)
        {
            await context.BudgetCategories
                .Where(bc => budgetIds.Contains(bc.BudgetId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // 8. Delete budgets
        await context.Budgets
            .Where(b => b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 9. Delete monthly budgets
        await context.MonthlyBudgets
            .Where(mb => mb.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 10. Delete categories
        await context.Categories
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 11. Delete category groups
        await context.CategoryGroups
            .Where(cg => cg.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 12. Delete payees
        await context.Payees
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 13. Delete saved filters
        await context.SavedTransactionFilters
            .Where(f => f.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 14. Delete user settings
        await context.UserSettings
            .Where(us => us.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserSettings(userId));
    }

    /// <inheritdoc />
    public async Task LoadDemoDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Create category groups and categories
        var incomeGroup = new CategoryGroup
        {
            UserId = userId,
            Name = "Income",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            Type = CategoryType.Income
        };
        var essentialsGroup = new CategoryGroup
        {
            UserId = userId,
            Name = "Essentials",
            SortOrder = 2,
            IsActive = true,
            CreatedAt = now,
            Type = CategoryType.Expense
        };
        var lifestyleGroup = new CategoryGroup
        {
            UserId = userId,
            Name = "Lifestyle",
            SortOrder = 3,
            IsActive = true,
            CreatedAt = now,
            Type = CategoryType.Expense
        };
        var transportGroup = new CategoryGroup
        {
            UserId = userId,
            Name = "Transport",
            SortOrder = 4,
            IsActive = true,
            CreatedAt = now,
            Type = CategoryType.Expense
        };

        context.CategoryGroups.AddRange(incomeGroup, essentialsGroup, lifestyleGroup, transportGroup);
        await context.SaveChangesAsync(cancellationToken);

        var salaryCategory = new Category { UserId = userId, CategoryGroupId = incomeGroup.Id, Name = "Salary", IsActive = true, CreatedAt = now };
        var freelanceCategory = new Category { UserId = userId, CategoryGroupId = incomeGroup.Id, Name = "Freelance", IsActive = true, CreatedAt = now };
        var groceriesCategory = new Category { UserId = userId, CategoryGroupId = essentialsGroup.Id, Name = "Groceries", IsActive = true, CreatedAt = now };
        var utilitiesCategory = new Category { UserId = userId, CategoryGroupId = essentialsGroup.Id, Name = "Utilities", IsActive = true, CreatedAt = now };
        var rentCategory = new Category { UserId = userId, CategoryGroupId = essentialsGroup.Id, Name = "Rent", IsActive = true, CreatedAt = now };
        var diningCategory = new Category { UserId = userId, CategoryGroupId = lifestyleGroup.Id, Name = "Dining Out", IsActive = true, CreatedAt = now };
        var entertainmentCategory = new Category { UserId = userId, CategoryGroupId = lifestyleGroup.Id, Name = "Entertainment", IsActive = true, CreatedAt = now };
        var shoppingCategory = new Category { UserId = userId, CategoryGroupId = lifestyleGroup.Id, Name = "Shopping", IsActive = true, CreatedAt = now };
        var subscriptionsCategory = new Category { UserId = userId, CategoryGroupId = lifestyleGroup.Id, Name = "Subscriptions", IsActive = true, CreatedAt = now };
        var gasCategory = new Category { UserId = userId, CategoryGroupId = transportGroup.Id, Name = "Gas", IsActive = true, CreatedAt = now };
        var publicTransportCategory = new Category { UserId = userId, CategoryGroupId = transportGroup.Id, Name = "Public Transport", IsActive = true, CreatedAt = now };

        context.Categories.AddRange(salaryCategory, freelanceCategory, groceriesCategory, utilitiesCategory, rentCategory,
            diningCategory, entertainmentCategory, shoppingCategory, subscriptionsCategory, gasCategory, publicTransportCategory);
        await context.SaveChangesAsync(cancellationToken);

        // Create payees
        var employer = new Payee { UserId = userId, Name = "Acme Corp", DefaultCategoryId = salaryCategory.Id, IsActive = true, CreatedAt = now };
        var supermarket = new Payee { UserId = userId, Name = "Fresh Mart", DefaultCategoryId = groceriesCategory.Id, IsActive = true, CreatedAt = now };
        var electricCompany = new Payee { UserId = userId, Name = "City Electric", DefaultCategoryId = utilitiesCategory.Id, IsActive = true, CreatedAt = now };
        var landlord = new Payee { UserId = userId, Name = "Urban Living Properties", DefaultCategoryId = rentCategory.Id, IsActive = true, CreatedAt = now };
        var restaurant1 = new Payee { UserId = userId, Name = "The Italian Place", DefaultCategoryId = diningCategory.Id, IsActive = true, CreatedAt = now };
        var restaurant2 = new Payee { UserId = userId, Name = "Sushi Express", DefaultCategoryId = diningCategory.Id, IsActive = true, CreatedAt = now };
        var netflix = new Payee { UserId = userId, Name = "Netflix", DefaultCategoryId = subscriptionsCategory.Id, IsActive = true, CreatedAt = now };
        var spotify = new Payee { UserId = userId, Name = "Spotify", DefaultCategoryId = subscriptionsCategory.Id, IsActive = true, CreatedAt = now };
        var gasStation = new Payee { UserId = userId, Name = "Shell Gas", DefaultCategoryId = gasCategory.Id, IsActive = true, CreatedAt = now };
        var amazon = new Payee { UserId = userId, Name = "Amazon", DefaultCategoryId = shoppingCategory.Id, IsActive = true, CreatedAt = now };
        var cinema = new Payee { UserId = userId, Name = "Cineplex", DefaultCategoryId = entertainmentCategory.Id, IsActive = true, CreatedAt = now };
        var transitAuthority = new Payee { UserId = userId, Name = "Metro Transit", DefaultCategoryId = publicTransportCategory.Id, IsActive = true, CreatedAt = now };

        context.Payees.AddRange(employer, supermarket, electricCompany, landlord, restaurant1, restaurant2,
            netflix, spotify, gasStation, amazon, cinema, transitAuthority);
        await context.SaveChangesAsync(cancellationToken);

        // Create accounts
        var checkingAccount = new Account
        {
            UserId = userId,
            Name = "Main Checking",
            Type = AccountType.Asset,
            SubType = AccountSubType.Checking,
            Group = "Personal",
            OpeningBalance = 5000m,
            MonthlySpendingLimit = 3000m,
            IsActive = true,
            CreatedAt = now
        };
        var savingsAccount = new Account
        {
            UserId = userId,
            Name = "Emergency Fund",
            Type = AccountType.Asset,
            SubType = AccountSubType.Savings,
            Group = "Personal",
            OpeningBalance = 15000m,
            IsActive = true,
            CreatedAt = now
        };
        var cashAccount = new Account
        {
            UserId = userId,
            Name = "Wallet Cash",
            Type = AccountType.Asset,
            SubType = AccountSubType.Cash,
            OpeningBalance = 200m,
            MonthlySpendingLimit = 500m,
            IsActive = true,
            CreatedAt = now
        };

        context.Accounts.AddRange(checkingAccount, savingsAccount, cashAccount);
        await context.SaveChangesAsync(cancellationToken);

        // Credit card needs to reference checking account for billing
        var creditCard = new Account
        {
            UserId = userId,
            Name = "Visa Platinum",
            Type = AccountType.Liability,
            SubType = AccountSubType.CreditCard,
            Group = "Personal",
            OpeningBalance = 0m,
            MonthlySpendingLimit = 5000m,
            BillingCycleDay = 15,
            LinkedPaymentAccountId = checkingAccount.Id,
            IsActive = true,
            CreatedAt = now
        };
        context.Accounts.Add(creditCard);
        await context.SaveChangesAsync(cancellationToken);

        // Create transactions for the last 3 months
        var transactions = new List<Transaction>();
        var baseDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int monthOffset = -2; monthOffset <= 0; monthOffset++)
        {
            var monthDate = baseDate.AddMonths(monthOffset);

            // Salary on the 1st
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = checkingAccount.Id,
                Date = monthDate,
                Amount = 4500m,
                PayeeId = employer.Id,
                CategoryId = salaryCategory.Id,
                Memo = "Monthly salary",
                Status = TransactionStatus.Posted,
                IsCleared = true,
                CreatedAt = now
            });

            // Rent on the 1st
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = checkingAccount.Id,
                Date = monthDate.AddDays(1),
                Amount = -1500m,
                PayeeId = landlord.Id,
                CategoryId = rentCategory.Id,
                Memo = "Monthly rent",
                Status = TransactionStatus.Posted,
                IsCleared = true,
                CreatedAt = now
            });

            // Utilities mid-month
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = checkingAccount.Id,
                Date = monthDate.AddDays(14),
                Amount = -120m,
                PayeeId = electricCompany.Id,
                CategoryId = utilitiesCategory.Id,
                Memo = "Electric bill",
                Status = TransactionStatus.Posted,
                IsCleared = true,
                CreatedAt = now
            });

            // Weekly groceries (4x per month)
            for (int week = 0; week < 4; week++)
            {
                transactions.Add(new Transaction
                {
                    UserId = userId,
                    AccountId = creditCard.Id,
                    Date = monthDate.AddDays(3 + (week * 7)),
                    Amount = -(85m + (week * 10)),
                    PayeeId = supermarket.Id,
                    CategoryId = groceriesCategory.Id,
                    Memo = "Weekly groceries",
                    Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                    IsCleared = monthOffset < 0,
                    CreatedAt = now
                });
            }

            // Gas twice a month
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(5),
                Amount = -55m,
                PayeeId = gasStation.Id,
                CategoryId = gasCategory.Id,
                Memo = "Gas fill-up",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(20),
                Amount = -48m,
                PayeeId = gasStation.Id,
                CategoryId = gasCategory.Id,
                Memo = "Gas fill-up",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });

            // Dining out
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(8),
                Amount = -62m,
                PayeeId = restaurant1.Id,
                CategoryId = diningCategory.Id,
                Memo = "Dinner with friends",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(22),
                Amount = -38m,
                PayeeId = restaurant2.Id,
                CategoryId = diningCategory.Id,
                Memo = "Lunch",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });

            // Subscriptions
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(1),
                Amount = -15.99m,
                PayeeId = netflix.Id,
                CategoryId = subscriptionsCategory.Id,
                Memo = "Monthly subscription",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = creditCard.Id,
                Date = monthDate.AddDays(1),
                Amount = -9.99m,
                PayeeId = spotify.Id,
                CategoryId = subscriptionsCategory.Id,
                Memo = "Premium subscription",
                Status = monthOffset == 0 ? TransactionStatus.Pending : TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });

            // Entertainment
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = cashAccount.Id,
                Date = monthDate.AddDays(12),
                Amount = -25m,
                PayeeId = cinema.Id,
                CategoryId = entertainmentCategory.Id,
                Memo = "Movie tickets",
                Status = TransactionStatus.Posted,
                IsCleared = true,
                CreatedAt = now
            });

            // Shopping
            if (monthOffset <= -1)
            {
                transactions.Add(new Transaction
                {
                    UserId = userId,
                    AccountId = creditCard.Id,
                    Date = monthDate.AddDays(18),
                    Amount = -89m,
                    PayeeId = amazon.Id,
                    CategoryId = shoppingCategory.Id,
                    Memo = "Online order",
                    Status = TransactionStatus.Posted,
                    IsCleared = true,
                    CreatedAt = now
                });
            }

            // Transit pass
            transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = checkingAccount.Id,
                Date = monthDate.AddDays(1),
                Amount = -75m,
                PayeeId = transitAuthority.Id,
                CategoryId = publicTransportCategory.Id,
                Memo = "Monthly transit pass",
                Status = TransactionStatus.Posted,
                IsCleared = monthOffset < 0,
                CreatedAt = now
            });
        }

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync(cancellationToken);

        // Create ledger entries for all transactions (required for dashboard/reporting)
        var ledgerEntries = new List<LedgerEntry>();
        foreach (var transaction in transactions)
        {
            // Determine account type
            var accountType = transaction.AccountId == checkingAccount.Id ||
                             transaction.AccountId == savingsAccount.Id ||
                             transaction.AccountId == cashAccount.Id
                ? AccountType.Asset
                : AccountType.Liability;

            var isIncome = transaction.Amount > 0;
            var absAmount = Math.Abs(transaction.Amount);

            if (isIncome)
            {
                // Income: Debit Asset (increase) / Credit Category
                if (accountType == AccountType.Asset)
                {
                    ledgerEntries.Add(new LedgerEntry
                    {
                        UserId = userId,
                        TransactionId = transaction.Id,
                        AccountId = transaction.AccountId,
                        CategoryId = null,
                        DebitAmount = absAmount,
                        CreditAmount = 0,
                        EntryDate = transaction.Date,
                        Description = transaction.Memo ?? "Income"
                    });
                }
                else
                {
                    // Payment toward liability
                    ledgerEntries.Add(new LedgerEntry
                    {
                        UserId = userId,
                        TransactionId = transaction.Id,
                        AccountId = transaction.AccountId,
                        CategoryId = null,
                        DebitAmount = absAmount,
                        CreditAmount = 0,
                        EntryDate = transaction.Date,
                        Description = transaction.Memo ?? "Payment toward liability"
                    });
                }

                // Credit Income Category
                ledgerEntries.Add(new LedgerEntry
                {
                    UserId = userId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = transaction.CategoryId,
                    DebitAmount = 0,
                    CreditAmount = absAmount,
                    EntryDate = transaction.Date,
                    Description = $"Income: {transaction.Memo ?? "Income"}"
                });
            }
            else
            {
                // Expense: Debit Category / Credit Asset (decrease) or Credit Liability (increase)

                // Debit Expense Category
                ledgerEntries.Add(new LedgerEntry
                {
                    UserId = userId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = transaction.CategoryId,
                    DebitAmount = absAmount,
                    CreditAmount = 0,
                    EntryDate = transaction.Date,
                    Description = $"Expense: {transaction.Memo ?? "Expense"}"
                });

                if (accountType == AccountType.Asset)
                {
                    // Credit Asset (decrease)
                    ledgerEntries.Add(new LedgerEntry
                    {
                        UserId = userId,
                        TransactionId = transaction.Id,
                        AccountId = transaction.AccountId,
                        CategoryId = null,
                        DebitAmount = 0,
                        CreditAmount = absAmount,
                        EntryDate = transaction.Date,
                        Description = transaction.Memo ?? "Expense"
                    });
                }
                else
                {
                    // Credit Liability (increase debt)
                    ledgerEntries.Add(new LedgerEntry
                    {
                        UserId = userId,
                        TransactionId = transaction.Id,
                        AccountId = transaction.AccountId,
                        CategoryId = null,
                        DebitAmount = 0,
                        CreditAmount = absAmount,
                        EntryDate = transaction.Date,
                        Description = transaction.Memo ?? "Charge to liability"
                    });
                }
            }
        }

        context.LedgerEntries.AddRange(ledgerEntries);
        await context.SaveChangesAsync(cancellationToken);

        // Create monthly budgets for current and past 2 months
        var currentMonth = now.Month;
        var currentYear = now.Year;

        var monthlyBudgets = new List<MonthlyBudget>();

        for (int monthOffset = -2; monthOffset <= 0; monthOffset++)
        {
            var budgetDate = new DateTime(currentYear, currentMonth, 1).AddMonths(monthOffset);
            var budgetYear = budgetDate.Year;
            var budgetMonth = budgetDate.Month;

            monthlyBudgets.AddRange(new[]
            {
                new MonthlyBudget { UserId = userId, CategoryId = groceriesCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 400m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = utilitiesCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 150m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = rentCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 1500m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = diningCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 150m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = entertainmentCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 100m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = shoppingCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 200m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = subscriptionsCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 50m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = gasCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 120m, IsDefault = false, CreatedAt = now },
                new MonthlyBudget { UserId = userId, CategoryId = publicTransportCategory.Id, Year = budgetYear, Month = budgetMonth, PlannedAmount = 80m, IsDefault = false, CreatedAt = now }
            });
        }

        context.MonthlyBudgets.AddRange(monthlyBudgets);
        await context.SaveChangesAsync(cancellationToken);

        // Create named budgets
        var monthlyBudget = new Budget
        {
            UserId = userId,
            Name = "Monthly Expenses",
            Description = "Standard monthly budget for living expenses",
            IsDefault = true,
            Year = null,
            Month = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var savingsBudget = new Budget
        {
            UserId = userId,
            Name = "Savings Goal",
            Description = "Track spending to maximize savings",
            IsDefault = false,
            Year = currentYear,
            Month = currentMonth,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Budgets.AddRange(monthlyBudget, savingsBudget);
        await context.SaveChangesAsync(cancellationToken);

        // Add budget categories for the monthly expenses budget
        var monthlyBudgetCategories = new List<BudgetCategory>
        {
            new() { BudgetId = monthlyBudget.Id, CategoryId = groceriesCategory.Id, PlannedAmount = 400m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = utilitiesCategory.Id, PlannedAmount = 150m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = rentCategory.Id, PlannedAmount = 1500m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = diningCategory.Id, PlannedAmount = 150m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = entertainmentCategory.Id, PlannedAmount = 100m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = shoppingCategory.Id, PlannedAmount = 200m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = subscriptionsCategory.Id, PlannedAmount = 50m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = gasCategory.Id, PlannedAmount = 120m },
            new() { BudgetId = monthlyBudget.Id, CategoryId = publicTransportCategory.Id, PlannedAmount = 80m }
        };

        // Add budget categories for the savings goal budget (tighter limits)
        var savingsBudgetCategories = new List<BudgetCategory>
        {
            new() { BudgetId = savingsBudget.Id, CategoryId = groceriesCategory.Id, PlannedAmount = 350m },
            new() { BudgetId = savingsBudget.Id, CategoryId = utilitiesCategory.Id, PlannedAmount = 130m },
            new() { BudgetId = savingsBudget.Id, CategoryId = rentCategory.Id, PlannedAmount = 1500m },
            new() { BudgetId = savingsBudget.Id, CategoryId = diningCategory.Id, PlannedAmount = 75m },
            new() { BudgetId = savingsBudget.Id, CategoryId = entertainmentCategory.Id, PlannedAmount = 50m },
            new() { BudgetId = savingsBudget.Id, CategoryId = shoppingCategory.Id, PlannedAmount = 100m },
            new() { BudgetId = savingsBudget.Id, CategoryId = subscriptionsCategory.Id, PlannedAmount = 30m },
            new() { BudgetId = savingsBudget.Id, CategoryId = gasCategory.Id, PlannedAmount = 100m },
            new() { BudgetId = savingsBudget.Id, CategoryId = publicTransportCategory.Id, PlannedAmount = 80m }
        };

        context.BudgetCategories.AddRange(monthlyBudgetCategories);
        context.BudgetCategories.AddRange(savingsBudgetCategories);
        await context.SaveChangesAsync(cancellationToken);
    }
}
