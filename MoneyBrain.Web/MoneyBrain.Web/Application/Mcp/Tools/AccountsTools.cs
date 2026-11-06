using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Account metadata, auditable adjustments and balance snapshots for the authenticated owner.</summary>
[McpServerToolType]
public sealed class AccountsTools(McpAccess access, IAccountService accounts, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists a bounded page of accounts without navigation properties.</summary>
    [McpServerTool(Name = "accounts_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned accounts, including inactive accounts only when requested. Offset paging, maximum 100.")]
    public async Task<FinancePage<AccountToolDto>> List(int offset = 0, int limit = 100, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await Project(db.Accounts.AsNoTracking().Where(a => a.UserId == user && (includeInactive || a.IsActive))
            .OrderBy(a => a.Id).Skip(offset).Take(limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Reads one account and its service-calculated balance.</summary>
    [McpServerTool(Name = "accounts_get", ReadOnly = true, OpenWorld = false)]
    [Description("Get account metadata and current balance. Monetary amounts are decimal in the account currency.")]
    public async Task<AccountBalanceToolDto> Get(int accountId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await Project(db.Accounts.AsNoTracking().Where(a => a.Id == accountId && a.UserId == user)).SingleOrDefaultAsync(cancellationToken);
        FinanceToolSupport.Check(row is not null, "Account not found.");
        var balance = await FinanceToolSupport.Run(() => accounts.CalculateCurrentBalanceAsync(accountId, user, cancellationToken));
        return new(row ?? throw new ModelContextProtocol.McpException("Account not found."), balance);
    }

    /// <summary>Creates an account; retries can create duplicates.</summary>
    [McpServerTool(Name = "accounts_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create an account. Requires confirmed=true and write permission. Not retry-safe; list accounts after a timeout before retrying.")]
    public async Task<AccountToolDto> Create(AccountWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Validate(input);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (input.LinkedPaymentAccountId is int linked) await FinanceToolSupport.Account(db, linked, user, cancellationToken);
        var account = new Account { UserId = user, Name = input.Name.Trim(), Type = input.Type, SubType = input.SubType, OpeningBalance = input.OpeningBalance };
        Apply(account, input);
        return Map(await FinanceToolSupport.Run(() => accounts.CreateAccountAsync(account, cancellationToken)));
    }

    /// <summary>Updates metadata without changing the audited opening balance.</summary>
    [McpServerTool(Name = "accounts_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Replace account metadata, preserving opening balance. Read accounts_get first. Use accounts_adjust_opening_balance for balance changes.")]
    public async Task<AccountToolDto> Update(int accountId, AccountWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Validate(input);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == user, cancellationToken)
            ?? throw new ModelContextProtocol.McpException("Account not found.");
        FinanceToolSupport.Check(input.LinkedPaymentAccountId != accountId, "An account cannot pay itself.");
        if (input.LinkedPaymentAccountId is int linked) await FinanceToolSupport.Account(db, linked, user, cancellationToken);
        Apply(account, input);
        return Map(await FinanceToolSupport.Run(() => accounts.UpdateAccountAsync(account, cancellationToken)));
    }

    /// <summary>Deactivates rather than deleting financial history.</summary>
    [McpServerTool(Name = "accounts_deactivate", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Deactivate an owned account without deleting history. Preview with accounts_get first.")]
    public async Task<MutationResult> Deactivate(int accountId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => accounts.DeactivateAccountAsync(accountId, user, cancellationToken)));
    }

    /// <summary>Deletes an empty account only; accounts with financial history must be archived.</summary>
    [McpServerTool(Name = "accounts_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete an empty owned account after accounts_get. Rejects any transaction, ledger, reconciliation, snapshot, audit, adjustment or payment-account reference. Use accounts_deactivate to preserve financial history.")]
    public async Task<MutationResult> Delete(int accountId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => accounts.DeleteAccountAsync(accountId, user, cancellationToken)));
    }

    /// <summary>Changes opening balance through the auditable application operation.</summary>
    [McpServerTool(Name = "accounts_adjust_opening_balance", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set an account opening balance with an audit reason. Preview accounts_get first. Repeating the same balance does not append another audit record.")]
    public async Task<AccountToolDto> AdjustOpeningBalance(int accountId, decimal newBalance, string reason, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Money(newBalance);
        reason = FinanceToolSupport.Text(reason, "Reason", 450);
        return Map(await FinanceToolSupport.Run(() => accounts.AdjustOpeningBalanceAsync(accountId, newBalance, reason, user, cancellationToken)));
    }

    /// <summary>Creates an explicit signed balance adjustment.</summary>
    [McpServerTool(Name = "accounts_adjustment_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create an auditable manual balance adjustment. Positive increases balance. Not retry-safe; read adjustments after a timeout.")]
    public async Task<AdjustmentToolDto> CreateAdjustment(int accountId, decimal amount, DateTime date, string description, string? category = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Money(amount);
        FinanceToolSupport.Date(date);
        description = FinanceToolSupport.Text(description, "Description", 500);
        FinanceToolSupport.OptionalText(category, "Category", 100);
        var a = await FinanceToolSupport.Run(() => accounts.CreateManualAdjustmentAsync(accountId, amount, date, description, category, user, cancellationToken));
        return new(a.Id, a.AccountId, a.Amount, a.AdjustmentDate, a.Description, a.Category, a.IsReconciled);
    }

    /// <summary>Deletes an unreconciled manual adjustment.</summary>
    [McpServerTool(Name = "accounts_adjustment_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete a manual adjustment after previewing accounts_adjustments_list. Reconciled adjustments cannot be deleted.")]
    public async Task<MutationResult> DeleteAdjustment(int adjustmentId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        FinanceToolSupport.Check(await db.ManualBalanceAdjustments.AnyAsync(a => a.Id == adjustmentId && a.Account != null && a.Account.UserId == user && !a.IsReconciled, cancellationToken), "Unreconciled adjustment not found.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => accounts.DeleteManualAdjustmentAsync(adjustmentId, user, cancellationToken)));
    }

    /// <summary>Lists a bounded page of manual adjustments.</summary>
    [McpServerTool(Name = "accounts_adjustments_list", ReadOnly = true, OpenWorld = false)]
    [Description("List manual balance adjustments for an owned account. Maximum 100 per page.")]
    public async Task<FinancePage<AdjustmentToolDto>> Adjustments(int accountId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, accountId, user, cancellationToken);
        var rows = await db.ManualBalanceAdjustments.AsNoTracking().Where(a => a.AccountId == accountId && a.Account != null && a.Account.UserId == user)
            .OrderByDescending(a => a.Id).Skip(offset).Take(limit + 1)
            .Select(a => new AdjustmentToolDto(a.Id, a.AccountId, a.Amount, a.AdjustmentDate, a.Description, a.Category, a.IsReconciled)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Reads opening balance audit history in bounded pages.</summary>
    [McpServerTool(Name = "accounts_opening_balance_history", ReadOnly = true, OpenWorld = false)]
    [Description("Read opening balance audit records. Maximum 100, newest first.")]
    public async Task<FinancePage<OpeningBalanceAuditToolDto>> OpeningHistory(int accountId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, accountId, user, cancellationToken);
        var rows = await db.OpeningBalanceAdjustments.AsNoTracking().Where(a => a.AccountId == accountId && a.Account != null && a.Account.UserId == user)
            .OrderByDescending(a => a.Id).Skip(offset).Take(limit + 1)
            .Select(a => new OpeningBalanceAuditToolDto(a.Id, a.PreviousBalance, a.NewBalance, a.AdjustmentAmount, a.Reason, a.AdjustedAt)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Records a user-provided manual snapshot, not an automatic balance calculation.</summary>
    [McpServerTool(Name = "accounts_snapshot_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Record a manual balance snapshot with ISO date. Does not change the account balance. Not retry-safe.")]
    public async Task<SnapshotToolDto> CreateSnapshot(int accountId, decimal balance, DateTime date, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Money(balance);
        FinanceToolSupport.Date(date);
        FinanceToolSupport.OptionalText(notes, "Notes");
        var s = await FinanceToolSupport.Run(() => accounts.CreateBalanceSnapshotAsync(accountId, balance, date, SnapshotType.Manual, notes, user, cancellationToken));
        return new(s.Id, s.AccountId, s.SnapshotDate, s.Balance, s.Type.ToString(), s.Notes);
    }

    /// <summary>Reads balance snapshots in SQL-paged order.</summary>
    [McpServerTool(Name = "accounts_snapshots_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned account balance snapshots. Maximum 100 per page.")]
    public async Task<FinancePage<SnapshotToolDto>> Snapshots(int accountId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, accountId, user, cancellationToken);
        var rows = await db.AccountBalanceSnapshots.AsNoTracking().Where(s => s.AccountId == accountId && s.Account != null && s.Account.UserId == user)
            .OrderByDescending(s => s.SnapshotDate).ThenByDescending(s => s.Id).Skip(offset).Take(limit + 1)
            .Select(s => new SnapshotToolDto(s.Id, s.AccountId, s.SnapshotDate, s.Balance, s.Type.ToString(), s.Notes)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    private static void Validate(AccountWriteInput input)
    {
        FinanceToolSupport.Validate(input);
        FinanceToolSupport.Money(input.OpeningBalance);
        if (input.MonthlySpendingLimit is decimal limit) FinanceToolSupport.Money(limit);
        FinanceToolSupport.Check(Enum.IsDefined(input.Type) && Enum.IsDefined(input.SubType), "Invalid account type.");
        FinanceToolSupport.Check(input.Type == AccountType.Asset ? (int)input.SubType <= 100 : (int)input.SubType > 100, "Account type and subtype must agree.");
    }

    private static void Apply(Account a, AccountWriteInput input)
    {
        a.Name = input.Name.Trim(); a.Type = input.Type; a.SubType = input.SubType;
        a.Group = input.Group; a.Notes = input.Notes; a.CurrencyCode = input.CurrencyCode;
        a.MonthlySpendingLimit = input.MonthlySpendingLimit; a.BillingCycleDay = input.BillingCycleDay;
        a.LinkedPaymentAccountId = input.LinkedPaymentAccountId; a.IsActive = input.IsActive;
    }

    private static IQueryable<AccountToolDto> Project(IQueryable<Account> rows)
        => rows.Select(a => new AccountToolDto(a.Id, a.Name, a.Type.ToString(), a.SubType.ToString(), a.Group,
            a.OpeningBalance, a.Notes, a.IsActive, a.CurrencyCode, a.MonthlySpendingLimit, a.BillingCycleDay, a.LinkedPaymentAccountId));

    private static AccountToolDto Map(Account a)
        => new(a.Id, a.Name, a.Type.ToString(), a.SubType.ToString(), a.Group, a.OpeningBalance, a.Notes,
            a.IsActive, a.CurrencyCode, a.MonthlySpendingLimit, a.BillingCycleDay, a.LinkedPaymentAccountId);
}

/// <summary>Account metadata input; opening balance is used on create only.</summary>
public sealed record AccountWriteInput(
    [property: Required, StringLength(100)] string Name,
    AccountType Type, AccountSubType SubType, decimal OpeningBalance = 0,
    [property: StringLength(50)] string? Group = null,
    [property: StringLength(500)] string? Notes = null,
    [property: RegularExpression("[A-Z]{3}")] string? CurrencyCode = null,
    [property: Range(typeof(decimal), "0", "999999999999999")] decimal? MonthlySpendingLimit = null,
    [property: Range(1, 31)] int? BillingCycleDay = null,
    int? LinkedPaymentAccountId = null, bool IsActive = true);

/// <summary>Account output with no identity or relationship graphs.</summary>
public sealed record AccountToolDto(int Id, string Name, string Type, string SubType, string? Group, decimal OpeningBalance,
    string? Notes, bool IsActive, string? CurrencyCode, decimal? MonthlySpendingLimit, int? BillingCycleDay, int? LinkedPaymentAccountId);
/// <summary>Account and current application-calculated balance.</summary>
public sealed record AccountBalanceToolDto(AccountToolDto Account, decimal CurrentBalance);
/// <summary>Manual balance adjustment, without creator identity.</summary>
public sealed record AdjustmentToolDto(int Id, int AccountId, decimal Amount, DateTime Date, string Description, string? Category, bool IsReconciled);
/// <summary>Balance snapshot output.</summary>
public sealed record SnapshotToolDto(int Id, int AccountId, DateTime Date, decimal Balance, string Type, string? Notes);
/// <summary>Audit record without user identity.</summary>
public sealed record OpeningBalanceAuditToolDto(int Id, decimal PreviousBalance, decimal NewBalance, decimal Change, string? Reason, DateTime Date);
