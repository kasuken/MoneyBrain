using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Transactions.PayeeNormalization;

/// <summary>
/// Manages payee entities: lookup, creation, duplicate detection, merging, renaming, and clean-up.
/// </summary>
public class PayeeService : IPayeeService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PayeeService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<List<Payee>> GetPayeesAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Payee> CreateOrGetPayeeAsync(string userId, string name, int? defaultCategoryId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Normalize the payee name
        var normalizedName = PayeeNormalizer.Normalize(name);
        var normalizedKey = PayeeNormalizer.GetNormalizedKey(normalizedName);

        // Load only the columns needed for normalized-key matching; the full entity
        // is not required because PayeeNormalizer only inspects the Name.
        var existingPayees = await context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var matchingPayeeId = existingPayees.FirstOrDefault(p =>
            PayeeNormalizer.GetNormalizedKey(p.Name) == normalizedKey)?.Id;

        if (matchingPayeeId.HasValue)
        {
            // Re-fetch full entity for callers that expect a complete Payee.
            // FindAsync uses the PK so it is effectively an identity lookup; null would
            // only happen in an extremely rare concurrent-delete race, which we treat as
            // "not found" and fall through to create a new payee.
            var existing = await context.Payees.FindAsync([matchingPayeeId.Value], cancellationToken);
            if (existing != null)
                return existing;
        }

        // Create new payee with normalized name
        var payee = new Payee
        {
            UserId = userId,
            Name = normalizedName,
            DefaultCategoryId = defaultCategoryId
        };

        context.Payees.Add(payee);
        await context.SaveChangesAsync(cancellationToken);

        return payee;
    }

    /// <inheritdoc />
    public async Task<List<PayeeWithUsage>> GetPayeesWithUsageAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var payees = await context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        if (payees.Count == 0)
            return new List<PayeeWithUsage>();

        var payeeIds = payees.Select(p => p.Id).ToList();

        var usageByPayeeId = await context.Transactions
            .Where(t => t.PayeeId.HasValue && payeeIds.Contains(t.PayeeId.Value))
            .GroupBy(t => t.PayeeId!.Value)
            .Select(g => new
            {
                PayeeId = g.Key,
                TransactionCount = g.Count(),
                LastUsedDate = g.Max(t => (DateTime?)t.Date)
            })
            .ToDictionaryAsync(x => x.PayeeId, cancellationToken);

        var payeesWithUsage = new List<PayeeWithUsage>(payees.Count);

        foreach (var payee in payees)
        {
            usageByPayeeId.TryGetValue(payee.Id, out var usage);

            payeesWithUsage.Add(new PayeeWithUsage
            {
                Payee = payee,
                TransactionCount = usage?.TransactionCount ?? 0,
                LastUsedDate = usage?.LastUsedDate
            });
        }

        return payeesWithUsage;
    }

    /// <inheritdoc />
    public async Task<List<PayeeDuplicateGroup>> FindDuplicatePayeesAsync(string userId, double similarityThreshold = 0.85, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var payeesWithUsage = await GetPayeesWithUsageAsync(userId, cancellationToken);

        // Group by normalized key
        var groups = new Dictionary<string, PayeeDuplicateGroup>();

        foreach (var payeeWithUsage in payeesWithUsage)
        {
            var normalizedKey = PayeeNormalizer.GetNormalizedKey(payeeWithUsage.Payee.Name);

            if (!groups.ContainsKey(normalizedKey))
            {
                groups[normalizedKey] = new PayeeDuplicateGroup
                {
                    NormalizedKey = normalizedKey
                };
            }

            groups[normalizedKey].Payees.Add(payeeWithUsage);
        }

        // Return only groups with duplicates
        return groups.Values
            .Where(g => g.HasDuplicates)
            .OrderByDescending(g => g.Payees.Sum(p => p.TransactionCount))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> MergePayeesAsync(string userId, int targetPayeeId, List<int> sourcePayeeIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(sourcePayeeIds);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var targetPayee = await context.Payees
            .FirstOrDefaultAsync(p => p.Id == targetPayeeId && p.UserId == userId, cancellationToken);

        if (targetPayee == null)
            return false;

        // Validate source payees
        var sourcePayees = await context.Payees
            .Where(p => sourcePayeeIds.Contains(p.Id) && p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (sourcePayees.Count != sourcePayeeIds.Count)
            return false;

        // Update all transactions from source payees to target payee
        var transactionsToUpdate = await context.Transactions
            .Where(t => t.PayeeId.HasValue && sourcePayeeIds.Contains(t.PayeeId.Value) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactionsToUpdate)
        {
            // Don't modify reconciled transactions
            if (!transaction.IsReconciled)
            {
                transaction.PayeeId = targetPayeeId;
                transaction.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Soft delete source payees
        foreach (var sourcePayee in sourcePayees)
        {
            sourcePayee.IsActive = false;
            sourcePayee.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RenamePayeeAsync(int payeeId, string userId, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var payee = await context.Payees
            .FirstOrDefaultAsync(p => p.Id == payeeId && p.UserId == userId, cancellationToken);

        if (payee == null)
            return false;

        // Normalize the new name
        payee.Name = PayeeNormalizer.Normalize(newName);
        payee.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteUnusedPayeesAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var unusedPayees = await context.Payees
            .Where(p => p.UserId == userId
                        && p.IsActive
                        && !context.Transactions.Any(t => t.PayeeId == p.Id))
            .ToListAsync(cancellationToken);

        // Soft delete unused payees
        foreach (var payee in unusedPayees)
        {
            payee.IsActive = false;
            payee.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return unusedPayees.Count;
    }
}
