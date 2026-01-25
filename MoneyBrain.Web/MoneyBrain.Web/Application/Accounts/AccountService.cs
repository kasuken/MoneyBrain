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

        // OpeningBalance should be updated with care; ideally via an adjustment mechanism
        // For v1, allow direct update but log it
        if (existing.OpeningBalance != account.OpeningBalance)
        {
            _logger.LogWarning(
                "Opening balance changed for account {AccountId} from {OldBalance} to {NewBalance}",
                account.Id,
                existing.OpeningBalance,
                account.OpeningBalance);
            existing.OpeningBalance = account.OpeningBalance;
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
}
