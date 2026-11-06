using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Mcp.Tools;

internal static class FinanceToolSupport
{
    internal static void Check([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new McpException(message);
    }

    internal static void Page(int skip, int take)
    {
        Check(skip is >= 0 and <= 1_000_000, "Offset must be between 0 and 1000000.");
        Check(take is >= 1 and <= 100, "Page size must be between 1 and 100.");
    }

    internal static string Text(string? value, string field, int maximum = 100)
    {
        Check(!string.IsNullOrWhiteSpace(value) && value.Length <= maximum, $"{field} is required and must be at most {maximum} characters.");
        return value.Trim();
    }

    internal static void OptionalText(string? value, string field, int maximum = 500)
        => Check(value is null || value.Length <= maximum, $"{field} must be at most {maximum} characters.");

    internal static void Validate(object? input)
    {
        Check(input is not null, "Input is required.");
        var errors = new List<ValidationResult>();
        Check(Validator.TryValidateObject(input, new ValidationContext(input), errors, true),
            "Input validation failed. Check required fields, lengths and numeric ranges.");
    }

    internal static void Period(int year, int month)
        => Check(year is >= 1900 and <= 9998 && month is >= 1 and <= 12, "Use a year between 1900 and 9998 and a month between 1 and 12.");

    internal static void Money(decimal value)
        => Check(value is >= -9_999_999_999_999_999.99m and <= 9_999_999_999_999_999.99m &&
            decimal.Round(value, 2) == value, "Money must fit decimal(18,2), with at most two fractional digits.");

    internal static void Date(DateTime value)
        => Check(value.Year is >= 1900 and < 9999, "Date must be between years 1900 and 9998.");

    internal static List<int> Ids(IReadOnlyCollection<int>? ids)
    {
        Check(ids is { Count: > 0 and <= 100 }, "Provide between 1 and 100 IDs.");
        Check(ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count, "IDs must be positive and unique.");
        return ids.ToList();
    }

    internal static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (ArgumentException) { throw new McpException("The operation contains invalid values. Check the input and retry."); }
        catch (UnauthorizedAccessException) { throw new McpException("The resource is not available to the authenticated user."); }
        catch (KeyNotFoundException) { throw new McpException("The requested resource was not found."); }
        catch (InvalidOperationException) { throw new McpException("The operation is not allowed in the current state. Refresh the resource and check ownership, reconciliation, references and duplicate names."); }
    }

    /// <summary>Maps a service's false result to an MCP error so a returned result always means the change was applied.</summary>
    internal static MutationResult Changed(bool changed)
    {
        Check(changed, "The requested resource was not found or the change could not be applied. Refresh it and retry.");
        return new(true);
    }
    internal static async Task Account(ApplicationDbContext db, int id, string user, CancellationToken ct)
        => Check(await db.Accounts.AnyAsync(x => x.Id == id && x.UserId == user, ct), "Account not found.");

    internal static async Task Category(ApplicationDbContext db, int? id, string user, CancellationToken ct)
    {
        if (id.HasValue)
            Check(await db.Categories.AnyAsync(x => x.Id == id && x.UserId == user && x.CategoryGroup.UserId == user, ct), "Category not found.");
    }

    internal static async Task Payee(ApplicationDbContext db, int? id, string user, CancellationToken ct)
    {
        if (id.HasValue)
            Check(await db.Payees.AnyAsync(x => x.Id == id && x.UserId == user, ct), "Payee not found.");
    }

    internal static async Task<Transaction> MutableTransaction(ApplicationDbContext db, int id, string user, CancellationToken ct, bool transfer = false)
    {
        var row = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == user && x.Account.UserId == user, ct);
        Check(row is not null, "Transaction not found.");
        Check(!row.IsReconciled, "Reconciled transactions are immutable.");
        Check(transfer || row.TransferTransactionId is null, "Use the transfer tools to change both sides together.");
        return row;
    }
}

/// <summary>Successful mutation outcome. Not-found or rejected changes are MCP errors, never Changed=false.</summary>
public sealed record MutationResult(bool Changed);

/// <summary>A bounded page. Request the next offset when HasMore is true.</summary>
public sealed record FinancePage<T>(IReadOnlyList<T> Items, int Offset, bool HasMore);

/// <summary>Safe transaction read model; dates are ISO 8601 and amounts are signed decimal values.</summary>
public sealed record FinanceTransactionDto(
    int Id, int AccountId, DateTime Date, decimal Amount, int? PayeeId, int? CategoryId,
    string? Memo, string Status, bool IsCleared, bool IsReconciled, int? TransferTransactionId,
    string? ReferenceNumber, string? Tags, bool IsRecurring, string? RecurrenceFrequency,
    DateTime? RecurrenceStartDate, DateTime? NextRecurrenceDate);

internal static class FinanceProjections
{
    internal static IQueryable<FinanceTransactionDto> Transactions(IQueryable<Transaction> rows)
        => rows.Select(t => new FinanceTransactionDto(t.Id, t.AccountId, t.Date, t.Amount, t.PayeeId,
            t.CategoryId, t.Memo, t.Status.ToString(), t.IsCleared, t.IsReconciled,
            t.TransferTransactionId, t.ReferenceNumber, t.Tags, t.IsRecurring,
            t.RecurrenceFrequency.HasValue ? t.RecurrenceFrequency.Value.ToString() : null,
            t.RecurrenceStartDate, t.NextRecurrenceDate));

    internal static FinanceTransactionDto Transaction(Transaction t)
        => new(t.Id, t.AccountId, t.Date, t.Amount, t.PayeeId, t.CategoryId, t.Memo,
            t.Status.ToString(), t.IsCleared, t.IsReconciled, t.TransferTransactionId,
            t.ReferenceNumber, t.Tags, t.IsRecurring, t.RecurrenceFrequency?.ToString(),
            t.RecurrenceStartDate, t.NextRecurrenceDate);
}
