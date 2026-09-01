using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.Ledger;

/// <summary>
/// Service for managing double-entry bookkeeping ledger entries.
/// Implements the fundamental accounting equation: Assets = Liabilities + Equity
/// 
/// Double-entry rules:
/// - Assets (checking, savings): Debit increases, Credit decreases
/// - Liabilities (credit cards, loans): Credit increases, Debit decreases
/// - Income: Credit increases (income received)
/// - Expenses: Debit increases (expense incurred)
/// - Every transaction must have balanced debits and credits (sum of debits = sum of credits)
/// </summary>
public class LedgerService(IDbContextFactory<ApplicationDbContext> contextFactory) : ILedgerService
{
    /// <summary>
    /// Generate ledger entries for a new transaction using the provided ambient context.
    /// </summary>
    public async Task GenerateLedgerEntriesAsync(ApplicationDbContext context, Transaction transaction, CancellationToken cancellationToken = default)
    {
        var entries = CreateLedgerEntries(transaction);
        
        // Validate that debits equal credits
        var totalDebits = entries.Sum(e => e.DebitAmount);
        var totalCredits = entries.Sum(e => e.CreditAmount);
        
        if (totalDebits != totalCredits)
        {
            throw new InvalidOperationException(
                $"Ledger entries are not balanced. Debits: {totalDebits:C}, Credits: {totalCredits:C}");
        }

        context.LedgerEntries.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Delete all ledger entries for a transaction.
    /// </summary>
    public async Task DeleteLedgerEntriesAsync(ApplicationDbContext context, int transactionId, CancellationToken cancellationToken = default)
    {
        var entries = await context.LedgerEntries
            .Where(le => le.TransactionId == transactionId)
            .ToListAsync(cancellationToken);

        context.LedgerEntries.RemoveRange(entries);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Regenerate ledger entries for a transaction.
    /// </summary>
    public async Task RegenerateLedgerEntriesAsync(ApplicationDbContext context, Transaction transaction, CancellationToken cancellationToken = default)
    {
        await DeleteLedgerEntriesAsync(context, transaction.Id, cancellationToken);
        await GenerateLedgerEntriesAsync(context, transaction, cancellationToken);
    }

    /// <summary>
    /// Get account balance based on ledger entries. Uses its own short-lived context.
    /// </summary>
    public async Task<decimal> GetAccountBalanceAsync(int accountId, string userId, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var account = await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}");
        }

        var query = context.LedgerEntries
            .Where(le => le.AccountId == accountId && le.UserId == userId && le.CategoryId == null);

        // Filter by date if specified
        if (asOfDate.HasValue)
        {
            query = query.Where(le => le.EntryDate <= asOfDate.Value);
        }

        var entries = await query.ToListAsync(cancellationToken);

        // Calculate balance based on account type
        decimal ledgerBalance;
        if (account.Type == AccountType.Asset)
        {
            // For assets: Balance increases with debits, decreases with credits
            ledgerBalance = entries.Sum(e => e.DebitAmount - e.CreditAmount);
        }
        else // Liability
        {
            // For liabilities: Balance increases with credits, decreases with debits
            ledgerBalance = entries.Sum(e => e.CreditAmount - e.DebitAmount);
        }

        // Get manual adjustments
        var adjustmentsQuery = context.ManualBalanceAdjustments
            .Where(mba => mba.AccountId == accountId);

        if (asOfDate.HasValue)
        {
            adjustmentsQuery = adjustmentsQuery.Where(mba => mba.AdjustmentDate <= asOfDate.Value);
        }

        var adjustmentsTotal = await adjustmentsQuery.SumAsync(mba => mba.Amount, cancellationToken);

        return account.OpeningBalance + ledgerBalance + adjustmentsTotal;
    }

    /// <summary>
    /// Create ledger entries for a transaction following double-entry bookkeeping rules.
    /// </summary>
    private List<LedgerEntry> CreateLedgerEntries(Transaction transaction)
    {
        var entries = new List<LedgerEntry>();

        // Check if this is a transfer (linked to another transaction)
        if (transaction.TransferTransactionId.HasValue)
        {
            // Transfer: Money moving between accounts
            // Determine if this is the "from" or "to" side based on amount sign
            if (transaction.Amount < 0)
            {
                // This is the "from" account (sending money) - Credit this account
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null, // Transfers don't affect categories
                    DebitAmount = 0,
                    CreditAmount = Math.Abs(transaction.Amount),
                    EntryDate = transaction.Date,
                    Description = $"Transfer to another account: {transaction.Memo ?? "Transfer"}"
                });
            }
            else
            {
                // This is the "to" account (receiving money) - Debit this account
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null,
                    DebitAmount = transaction.Amount,
                    CreditAmount = 0,
                    EntryDate = transaction.Date,
                    Description = $"Transfer from another account: {transaction.Memo ?? "Transfer"}"
                });
            }
        }
        else if (transaction.Splits.Any())
        {
            // Split transaction: Multiple categories
            foreach (var split in transaction.Splits)
            {
                CreateEntriesForAmount(entries, transaction, split.Amount, split.CategoryId, split.Memo);
            }
        }
        else
        {
            // Regular transaction (income or expense)
            CreateEntriesForAmount(entries, transaction, transaction.Amount, transaction.CategoryId, transaction.Memo);
        }

        return entries;
    }

    /// <summary>
    /// Create ledger entries for a specific amount within a transaction.
    /// Handles both income (positive) and expense (negative) amounts.
    /// </summary>
    private void CreateEntriesForAmount(
        List<LedgerEntry> entries, 
        Transaction transaction, 
        decimal amount, 
        int? categoryId, 
        string? description)
    {
        var accountType = transaction.Account?.Type ?? AccountType.Asset;
        var isIncome = amount > 0;
        var absAmount = Math.Abs(amount);

        if (isIncome)
        {
            // Income transaction
            // Debit: Asset Account (increase) or Credit: Liability Account (decrease debt)
            // Credit: Income Category
            
            if (accountType == AccountType.Asset)
            {
                // Debit Asset (increase)
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null,
                    DebitAmount = absAmount,
                    CreditAmount = 0,
                    EntryDate = transaction.Date,
                    Description = description ?? transaction.Memo ?? "Income"
                });
            }
            else // Liability
            {
                // Debit Liability (decrease debt - payment toward liability)
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null,
                    DebitAmount = absAmount,
                    CreditAmount = 0,
                    EntryDate = transaction.Date,
                    Description = description ?? transaction.Memo ?? "Payment toward liability"
                });
            }

            // Credit Income Category (virtual account representing income source)
            entries.Add(new LedgerEntry
            {
                UserId = transaction.UserId,
                TransactionId = transaction.Id,
                AccountId = transaction.AccountId, // Store account reference
                CategoryId = categoryId,
                DebitAmount = 0,
                CreditAmount = absAmount,
                EntryDate = transaction.Date,
                Description = $"Income: {description ?? transaction.Memo ?? "Income"}"
            });
        }
        else
        {
            // Expense transaction
            // Debit: Expense Category
            // Credit: Asset Account (decrease) or Debit: Liability Account (increase debt)

            // Debit Expense Category (virtual account representing expense)
            entries.Add(new LedgerEntry
            {
                UserId = transaction.UserId,
                TransactionId = transaction.Id,
                AccountId = transaction.AccountId, // Store account reference
                CategoryId = categoryId,
                DebitAmount = absAmount,
                CreditAmount = 0,
                EntryDate = transaction.Date,
                Description = $"Expense: {description ?? transaction.Memo ?? "Expense"}"
            });

            if (accountType == AccountType.Asset)
            {
                // Credit Asset (decrease)
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null,
                    DebitAmount = 0,
                    CreditAmount = absAmount,
                    EntryDate = transaction.Date,
                    Description = description ?? transaction.Memo ?? "Expense"
                });
            }
            else // Liability
            {
                // Credit Liability (increase debt - charging to credit card)
                entries.Add(new LedgerEntry
                {
                    UserId = transaction.UserId,
                    TransactionId = transaction.Id,
                    AccountId = transaction.AccountId,
                    CategoryId = null,
                    DebitAmount = 0,
                    CreditAmount = absAmount,
                    EntryDate = transaction.Date,
                    Description = description ?? transaction.Memo ?? "Charge to liability"
                });
            }
        }
    }
}
