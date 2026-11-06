using System.Resources;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Mcp.Tools;

internal static class ReportInput
{
    private static readonly ResourceManager Messages =
        new("MoneyBrain.Web.Resources.McpReportMessages", typeof(ReportInput).Assembly);

    internal static McpException Error(string key) => new(Messages.GetString(key)
        ?? throw new InvalidOperationException($"Missing MCP resource: {key}"));

    internal static void Range(DateTime startDate, DateTime endDate, int? intervalDays = null)
    {
        if (startDate.Year < 1900 || endDate.Year > 9998 ||
            startDate > endDate || (endDate - startDate).TotalDays > 366)
            throw Error("InvalidRange");

        if (intervalDays is { } interval &&
            (interval < 1 || interval > 366 || (endDate - startDate).TotalDays / interval > 119))
            throw Error("InvalidInterval");
    }

    internal static void Month(int year, int month)
    {
        if (year < 1900 || year > 9998 || month < 1 || month > 12)
            throw Error("InvalidMonth");
    }

    internal static void Page(int page, int pageSize)
    {
        if (page < 1 || page > 10000 || pageSize < 1 || pageSize > 100)
            throw Error("InvalidPage");
    }

    internal static async Task RequireBoundedDataAsync(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        string userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Accounts.CountAsync(a => a.UserId == userId, cancellationToken) > 200 ||
            await db.Categories.CountAsync(c => c.UserId == userId, cancellationToken) > 200 ||
            await db.LedgerEntries.Where(e => e.UserId == userId && e.EntryDate >= startDate && e.EntryDate <= endDate).Take(10001).CountAsync(cancellationToken) > 10000 ||
            await db.Transactions.Where(t => t.UserId == userId && t.Date >= startDate && t.Date <= endDate).Take(10001).CountAsync(cancellationToken) > 10000)
            throw Error("ReportTooLarge");
    }

    internal static string Disclaimer => Messages.GetString("Disclaimer")
        ?? throw new InvalidOperationException("Missing MCP disclaimer resource.");
}
