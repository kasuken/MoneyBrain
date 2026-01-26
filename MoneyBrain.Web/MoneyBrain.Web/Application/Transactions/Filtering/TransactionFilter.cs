using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.Filtering;

/// <summary>
/// Filter criteria for searching transactions
/// </summary>
public class TransactionFilter
{
    /// <summary>
    /// Account ID to filter by (null = all accounts)
    /// </summary>
    public int? AccountId { get; set; }

    /// <summary>
    /// Search text for payee, memo, category, or reference number
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Start date for date range filter
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for date range filter
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Minimum amount (null = no minimum)
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Maximum amount (null = no maximum)
    /// </summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Filter by transaction type (null = all)
    /// </summary>
    public TransactionType? TransactionType { get; set; }

    /// <summary>
    /// Filter by category IDs
    /// </summary>
    public List<int>? CategoryIds { get; set; }

    /// <summary>
    /// Filter by payee IDs
    /// </summary>
    public List<int>? PayeeIds { get; set; }

    /// <summary>
    /// Filter by transaction status
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Filter by cleared flag (null = all)
    /// </summary>
    public bool? IsCleared { get; set; }

    /// <summary>
    /// Filter by reconciled flag (null = all)
    /// </summary>
    public bool? IsReconciled { get; set; }

    /// <summary>
    /// Include transfers (default true)
    /// </summary>
    public bool IncludeTransfers { get; set; } = true;

    /// <summary>
    /// Tags to filter by
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Check if any filters are active
    /// </summary>
    public bool HasActiveFilters =>
        AccountId.HasValue ||
        !string.IsNullOrWhiteSpace(SearchText) ||
        StartDate.HasValue ||
        EndDate.HasValue ||
        MinAmount.HasValue ||
        MaxAmount.HasValue ||
        TransactionType.HasValue ||
        (CategoryIds?.Count ?? 0) > 0 ||
        (PayeeIds?.Count ?? 0) > 0 ||
        Status.HasValue ||
        IsCleared.HasValue ||
        IsReconciled.HasValue ||
        !IncludeTransfers ||
        (Tags?.Count ?? 0) > 0;

    /// <summary>
    /// Clear all filters
    /// </summary>
    public void Clear()
    {
        AccountId = null;
        SearchText = null;
        StartDate = null;
        EndDate = null;
        MinAmount = null;
        MaxAmount = null;
        TransactionType = null;
        CategoryIds = null;
        PayeeIds = null;
        Status = null;
        IsCleared = null;
        IsReconciled = null;
        IncludeTransfers = true;
        Tags = null;
    }
}

/// <summary>
/// Transaction type for filtering
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Income (positive amount)
    /// </summary>
    Income,
    
    /// <summary>
    /// Expense (negative amount)
    /// </summary>
    Expense,
    
    /// <summary>
    /// Transfer (linked to another transaction)
    /// </summary>
    Transfer
}
