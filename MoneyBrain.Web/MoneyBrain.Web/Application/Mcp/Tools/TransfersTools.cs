using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>First-class transfers; always operates on both sides through the transfer application service.</summary>
[McpServerToolType]
public sealed class TransfersTools(McpAccess access, ITransferService transfers, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Reads both owned transfer sides without returning navigation graphs.</summary>
    [McpServerTool(Name = "transfers_get", ReadOnly = true, OpenWorld = false)]
    [Description("Read both sides of an owned transfer by either transaction ID. This is the preview for update/delete.")]
    public async Task<TransferToolDto> Get(int transactionId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Read(db, user, transactionId, cancellationToken);
    }

    /// <summary>Creates exactly two linked transfer entries atomically.</summary>
    [McpServerTool(Name = "transfers_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Transfer a positive amount between two different owned accounts. Both sides are posted, excluded from income/expense, and created atomically. Not retry-safe: search transfers after timeout.")]
    public async Task<TransferToolDto> Create(TransferToolInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Validate(db, user, input, cancellationToken);
        var result = await FinanceToolSupport.Run(() => transfers.CreateTransferAsync(user, Map(input), cancellationToken));
        return new(FinanceProjections.Transaction(result.FromTransaction), FinanceProjections.Transaction(result.ToTransaction));
    }

    /// <summary>Updates both transfer sides without allowing account reassignment.</summary>
    [McpServerTool(Name = "transfers_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Update a transfer's date, amount, memo/reference and cleared flag after transfers_get. Supply the negative/from transaction ID and original account IDs. Reconciled sides cannot change.")]
    public async Task<MutationResult> Update(int fromTransactionId, TransferToolInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Validate(db, user, input, cancellationToken);
        var pair = await Read(db, user, fromTransactionId, cancellationToken);
        FinanceToolSupport.Check(pair.From.Id == fromTransactionId && pair.From.AccountId == input.FromAccountId && pair.To.AccountId == input.ToAccountId,
            "Supply the original from transaction and account IDs; account reassignment is not supported.");
        FinanceToolSupport.Check(!pair.From.IsReconciled && !pair.To.IsReconciled, "Reconciled transfers are immutable.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => transfers.UpdateTransferAsync(user, fromTransactionId, Map(input), cancellationToken)));
    }

    /// <summary>Deletes both transfer sides together.</summary>
    [McpServerTool(Name = "transfers_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete both sides after transfers_get. Either reconciled side prevents deletion. Never delete an individual side with transaction tools.")]
    public async Task<MutationResult> Delete(int transactionId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pair = await Read(db, user, transactionId, cancellationToken);
        FinanceToolSupport.Check(!pair.From.IsReconciled && !pair.To.IsReconciled, "Reconciled transfers are immutable.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => transfers.DeleteTransferAsync(user, transactionId, cancellationToken)));
    }

    private static async Task Validate(ApplicationDbContext db, string user, TransferToolInput input, CancellationToken ct)
    {
        FinanceToolSupport.Validate(input);
        FinanceToolSupport.Money(input.Amount);
        FinanceToolSupport.Check(input.FromAccountId != input.ToAccountId && input.Amount > 0, "Transfer requires different accounts and a positive amount.");
        FinanceToolSupport.Check(input.Date.Year is >= 1900 and < 9999, "Invalid transfer date.");
        await FinanceToolSupport.Account(db, input.FromAccountId, user, ct);
        await FinanceToolSupport.Account(db, input.ToAccountId, user, ct);
    }

    private static async Task<TransferToolDto> Read(ApplicationDbContext db, string user, int id, CancellationToken ct)
    {
        var first = await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => t.Id == id && t.UserId == user && t.Account.UserId == user))
            .SingleOrDefaultAsync(ct) ?? throw new ModelContextProtocol.McpException("Transfer not found.");
        FinanceToolSupport.Check(first.TransferTransactionId.HasValue, "Transaction is not a transfer.");
        var second = await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => t.Id == first.TransferTransactionId && t.UserId == user && t.Account.UserId == user))
            .SingleOrDefaultAsync(ct) ?? throw new ModelContextProtocol.McpException("Linked transfer not found.");
        FinanceToolSupport.Check(second.TransferTransactionId == first.Id && first.AccountId != second.AccountId && first.Amount == -second.Amount && first.Amount != 0,
            "Transfer is not a valid linked pair.");
        return first.Amount < 0 ? new(first, second) : new(second, first);
    }

    private static TransferDto Map(TransferToolInput input) => new()
    {
        FromAccountId = input.FromAccountId, ToAccountId = input.ToAccountId, Amount = input.Amount,
        Date = input.Date, Memo = input.Memo, ReferenceNumber = input.ReferenceNumber, IsCleared = input.IsCleared
    };
}

/// <summary>Transfer fields with positive decimal amount and ISO date.</summary>
public sealed record TransferToolInput(int FromAccountId, int ToAccountId, decimal Amount, DateTime Date,
    [property: StringLength(1000)] string? Memo = null, [property: StringLength(100)] string? ReferenceNumber = null, bool IsCleared = false);
/// <summary>Safe debit and credit transfer sides.</summary>
public sealed record TransferToolDto(FinanceTransactionDto From, FinanceTransactionDto To);
