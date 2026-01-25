using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Accounts;

/// <summary>
/// Service for managing user accounts (assets and liabilities).
/// Enforces business rules and user ownership.
/// </summary>
public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountService> _logger;

    public AccountService(ApplicationDbContext context, ILogger<AccountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Account>> GetUserAccountsAsync(
        string userId, 
        bool includeInactive = false, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Accounts
            .Where(a => a.UserId == userId);

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        return await query
            .OrderBy(a => a.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetAccountByIdAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);
    }

    public async Task<Account> CreateAccountAsync(
        Account account, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (string.IsNullOrWhiteSpace(account.UserId))
        {
            throw new InvalidOperationException("Account must belong to a user.");
        }

        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = null;

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account created: {AccountId} - {AccountName} for user {UserId}", 
            account.Id, 
            account.Name, 
            account.UserId);

        return account;
    }

    public async Task<Account> UpdateAccountAsync(
        Account account, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var existing = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == account.Id && a.UserId == account.UserId, 
                cancellationToken);

        if (existing == null)
        {
            throw new InvalidOperationException($"Account {account.Id} not found for user {account.UserId}.");
        }

        // Update mutable fields
        existing.Name = account.Name;
        existing.Type = account.Type;
        existing.SubType = account.SubType;
        existing.Group = account.Group;
        existing.Notes = account.Notes;
        existing.IsActive = account.IsActive;
        existing.CurrencyCode = account.CurrencyCode;
        existing.UpdatedAt = DateTime.UtcNow;

        // OpeningBalance should NOT be updated directly - must use AdjustOpeningBalanceAsync
        // This enforces the audit trail requirement from the PRD
        if (existing.OpeningBalance != account.OpeningBalance)
        {
            _logger.LogWarning(
                "Attempted to change opening balance directly for account {AccountId}. Use AdjustOpeningBalanceAsync instead.",
                account.Id);
            throw new InvalidOperationException("Opening balance cannot be changed directly. Use AdjustOpeningBalanceAsync to maintain audit trail.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account updated: {AccountId} - {AccountName}", 
            account.Id, 
            account.Name);

        return existing;
    }

    public async Task<bool> DeactivateAccountAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            return false;
        }

        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account deactivated: {AccountId} - {AccountName}", 
            accountId, 
            account.Name);

        return true;
    }

    public async Task<bool> DeleteAccountAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            return false;
        }

        // TODO: Check if account has transactions before allowing deletion
        // For v1, allow deletion; add transaction check in future iteration

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account deleted: {AccountId} - {AccountName}", 
            accountId, 
            account.Name);

        return true;
    }

    public async Task<Account> AdjustOpeningBalanceAsync(
        int accountId, 
        decimal newBalance, 
        string? reason, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        var previousBalance = account.OpeningBalance;
        var adjustmentAmount = newBalance - previousBalance;

        // Only create adjustment record if the balance actually changed
        if (adjustmentAmount != 0)
        {
            var adjustment = new OpeningBalanceAdjustment
            {
                AccountId = accountId,
                PreviousBalance = previousBalance,
                NewBalance = newBalance,
                AdjustmentAmount = adjustmentAmount,
                Reason = reason,
                AdjustedAt = DateTime.UtcNow,
                AdjustedByUserId = userId
            };

            account.OpeningBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;

            _context.OpeningBalanceAdjustments.Add(adjustment);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Opening balance adjusted for account {AccountId} from {PreviousBalance} to {NewBalance} (Δ {Adjustment}). Reason: {Reason}",
                accountId,
                previousBalance,
                newBalance,
                adjustmentAmount,
                reason ?? "(none provided)");
        }
        else
        {
            _logger.LogDebug(
                "Opening balance adjustment requested for account {AccountId} but new balance equals current balance ({Balance}).",
                accountId,
                newBalance);
        }

        return account;
    }

    public async Task<IReadOnlyList<OpeningBalanceAdjustment>> GetOpeningBalanceAdjustmentsAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        return await _context.OpeningBalanceAdjustments
            .Where(oba => oba.AccountId == accountId)
            .OrderByDescending(oba => oba.AdjustedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
