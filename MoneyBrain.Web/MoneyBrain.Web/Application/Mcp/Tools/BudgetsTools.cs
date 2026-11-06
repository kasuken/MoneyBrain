using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Budgets;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Named budgets, allocations, and monthly/default envelope assignments.</summary>
[McpServerToolType]
public sealed class BudgetsTools(McpAccess access, IBudgetService budgets, IMonthlyBudgetService monthly, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists named budgets with optional period filters.</summary>
    [McpServerTool(Name = "budgets_list", ReadOnly = true, OpenWorld = false)]
    [Description("List named budgets, optionally by year/month or default flag. Maximum 100 per page.")]
    public async Task<FinancePage<BudgetToolDto>> List(int? year = null, int? month = null, bool? isDefault = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        if (year.HasValue || month.HasValue) FinanceToolSupport.Period(year ?? 0, month ?? 0);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await Project(db.Budgets.AsNoTracking().Where(b => b.UserId == user &&
                (year == null || b.Year == year && b.Month == month) && (isDefault == null || b.IsDefault == isDefault))
            .OrderBy(b => b.Id).Skip(offset).Take(limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Gets named budget metadata; allocations are separately paged.</summary>
    [McpServerTool(Name = "budgets_get", ReadOnly = true, OpenWorld = false)]
    [Description("Get named budget metadata. Use budgets_allocations_list for its bounded allocation list.")]
    public async Task<BudgetToolDto> Get(int budgetId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Project(db.Budgets.AsNoTracking().Where(b => b.Id == budgetId && b.UserId == user)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ModelContextProtocol.McpException("Budget not found.");
    }

    /// <summary>Lists allocations in one owned budget.</summary>
    [McpServerTool(Name = "budgets_allocations_list", ReadOnly = true, OpenWorld = false)]
    [Description("List category allocations for a named budget. Maximum 100 per page.")]
    public async Task<FinancePage<BudgetAllocationToolDto>> Allocations(int budgetId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await OwnedBudget(db, budgetId, user, cancellationToken);
        var rows = await db.BudgetCategories.AsNoTracking().Where(b => b.BudgetId == budgetId && b.Budget.UserId == user && b.Category.UserId == user)
            .OrderBy(b => b.Id).Skip(offset).Take(limit + 1)
            .Select(b => new BudgetAllocationToolDto(b.Id, b.BudgetId, b.CategoryId, b.PlannedAmount, b.AllowRollover, b.Notes)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Creates a named default or period budget.</summary>
    [McpServerTool(Name = "budgets_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a named budget. Default budgets omit year/month; period budgets require both. Not retry-safe.")]
    public async Task<BudgetToolDto> Create(BudgetWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Validate(input);
        return Map(await FinanceToolSupport.Run(() => budgets.CreateBudgetAsync(user, input.Name.Trim(), input.Description, input.IsDefault, input.Year, input.Month, cancellationToken)));
    }

    /// <summary>Updates named budget metadata.</summary>
    [McpServerTool(Name = "budgets_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Replace named budget metadata. Read budgets_get first. Does not replace allocations.")]
    public async Task<BudgetToolDto> Update(int budgetId, BudgetWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Validate(input);
        return Map(await FinanceToolSupport.Run(() => budgets.UpdateBudgetAsync(budgetId, user, input.Name.Trim(), input.Description, input.IsDefault, input.Year, input.Month, cancellationToken)));
    }

    /// <summary>Deletes an owned named budget and allocations.</summary>
    [McpServerTool(Name = "budgets_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete a named budget and its allocations. Preview budgets_get and budgets_allocations_list first; transactions are unchanged.")]
    public async Task<MutationResult> Delete(int budgetId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => budgets.DeleteBudgetAsync(budgetId, user, cancellationToken)));
    }

    /// <summary>Adds an owned category allocation.</summary>
    [McpServerTool(Name = "budgets_allocation_add", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Add an owned category to a named budget. Nonnegative planned amount. Duplicate allocation is rejected.")]
    public async Task<BudgetAllocationToolDto> AddAllocation(int budgetId, int categoryId, decimal plannedAmount, bool allowRollover = false, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Amount(plannedAmount, notes);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await OwnedBudget(db, budgetId, user, cancellationToken);
        await FinanceToolSupport.Category(db, categoryId, user, cancellationToken);
        var b = await FinanceToolSupport.Run(() => budgets.AddCategoryToBudgetAsync(budgetId, user, categoryId, plannedAmount, allowRollover, notes, cancellationToken));
        return new(b.Id, b.BudgetId, b.CategoryId, b.PlannedAmount, b.AllowRollover, b.Notes);
    }

    /// <summary>Sets existing allocation values.</summary>
    [McpServerTool(Name = "budgets_allocation_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set an allocation's amount, rollover and notes after budgets_allocations_list.")]
    public async Task<BudgetAllocationToolDto> UpdateAllocation(int allocationId, decimal plannedAmount, bool allowRollover = false, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Amount(plannedAmount, notes);
        var b = await FinanceToolSupport.Run(() => budgets.UpdateBudgetCategoryAsync(allocationId, user, plannedAmount, allowRollover, notes, cancellationToken));
        return new(b.Id, b.BudgetId, b.CategoryId, b.PlannedAmount, b.AllowRollover, b.Notes);
    }

    /// <summary>Removes one allocation without changing category or transactions.</summary>
    [McpServerTool(Name = "budgets_allocation_remove", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Remove a named budget allocation after previewing budgets_allocations_list.")]
    public async Task<MutationResult> RemoveAllocation(int allocationId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => budgets.RemoveCategoryFromBudgetAsync(allocationId, user, cancellationToken)));
    }

    /// <summary>Creates a named budget from a supported built-in template.</summary>
    [McpServerTool(Name = "budgets_template_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a period budget from built-in template: Basic Essentials, Standard Living, Savings Focused, Family Budget, or Student Budget. Matches existing active category names only; list budgets/allocations afterward. Not retry-safe.")]
    public async Task<BudgetToolDto> FromTemplate(string templateName, int year, int month, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Period(year, month);
        FinanceToolSupport.Check(templateName is "Basic Essentials" or "Standard Living" or "Savings Focused" or "Family Budget" or "Student Budget", "Unsupported budget template.");
        return Map(await FinanceToolSupport.Run(() => budgets.CreateBudgetFromTemplateAsync(user, templateName, year, month, cancellationToken)));
    }

    /// <summary>Lists monthly/default assignments, optionally resolving defaults against overrides.</summary>
    [McpServerTool(Name = "monthly_budgets_list", ReadOnly = true, OpenWorld = false)]
    [Description("List default or month-specific envelope assignments. For a period, effective=true resolves category overrides over defaults. Maximum 100 per page.")]
    public async Task<FinancePage<MonthlyBudgetToolDto>> Monthly(int? year = null, int? month = null, bool effective = true, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        if (year.HasValue || month.HasValue) FinanceToolSupport.Period(year ?? 0, month ?? 0);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.MonthlyBudgets.AsNoTracking().Where(b => b.UserId == user && b.Category.UserId == user);
        if (!year.HasValue) query = query.Where(b => b.IsDefault);
        else if (!effective) query = query.Where(b => !b.IsDefault && b.Year == year && b.Month == month);
        else query = query.Where(b => !b.IsDefault && b.Year == year && b.Month == month ||
            b.IsDefault && !db.MonthlyBudgets.Any(o => o.UserId == user && o.CategoryId == b.CategoryId && !o.IsDefault && o.Year == year && o.Month == month));
        var rows = await query.OrderBy(b => b.CategoryId).ThenBy(b => b.Id).Skip(offset).Take(limit + 1)
            .Select(b => new MonthlyBudgetToolDto(b.Id, b.CategoryId, b.IsDefault, b.Year, b.Month, b.PlannedAmount, b.AllowRollover, b.Notes)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Upserts a default monthly assignment.</summary>
    [McpServerTool(Name = "monthly_budgets_set_default", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set a category's default envelope amount. Month overrides remain unchanged. Preview monthly_budgets_list first.")]
    public async Task<MonthlyBudgetToolDto> SetDefault(int categoryId, decimal plannedAmount, bool allowRollover = false, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        Amount(plannedAmount, notes);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Category(db, categoryId, user, cancellationToken);
        return Map(await FinanceToolSupport.Run(() => monthly.SetDefaultBudgetAsync(categoryId, user, plannedAmount, allowRollover, notes, cancellationToken)));
    }

    /// <summary>Upserts a month-specific assignment.</summary>
    [McpServerTool(Name = "monthly_budgets_set_override", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set one month/category envelope override. Preview monthly_budgets_list first.")]
    public async Task<MonthlyBudgetToolDto> SetOverride(int categoryId, int year, int month, decimal plannedAmount, bool allowRollover = false, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Period(year, month);
        Amount(plannedAmount, notes);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Category(db, categoryId, user, cancellationToken);
        return Map(await FinanceToolSupport.Run(() => monthly.SetMonthOverrideAsync(categoryId, user, year, month, plannedAmount, allowRollover, notes, cancellationToken)));
    }

    /// <summary>Removes a default or month-specific assignment.</summary>
    [McpServerTool(Name = "monthly_budgets_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete a default assignment when year/month are absent, or one month override when both are supplied. Deleting an override reveals the default. Preview monthly_budgets_list first.")]
    public async Task<MutationResult> DeleteMonthly(int categoryId, int? year = null, int? month = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        if (year.HasValue || month.HasValue) FinanceToolSupport.Period(year ?? 0, month ?? 0);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Category(db, categoryId, user, cancellationToken);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => year.HasValue
            ? monthly.DeleteMonthOverrideAsync(categoryId, user, year.Value, month.GetValueOrDefault(), cancellationToken)
            : monthly.DeleteDefaultBudgetAsync(categoryId, user, cancellationToken)));
    }

    private static void Validate(BudgetWriteInput input)
    {
        FinanceToolSupport.Validate(input);
        if (!input.IsDefault) FinanceToolSupport.Period(input.Year ?? 0, input.Month ?? 0);
        else FinanceToolSupport.Check(input.Year is null && input.Month is null, "Default budgets must omit year and month.");
    }
    private static void Amount(decimal amount, string? notes)
    {
        FinanceToolSupport.Money(amount);
        FinanceToolSupport.Check(amount >= 0, "Planned amount cannot be negative.");
        FinanceToolSupport.OptionalText(notes, "Notes", 1000);
    }
    private static async Task OwnedBudget(ApplicationDbContext db, int id, string user, CancellationToken ct)
        => FinanceToolSupport.Check(await db.Budgets.AnyAsync(b => b.Id == id && b.UserId == user, ct), "Budget not found.");
    private static IQueryable<BudgetToolDto> Project(IQueryable<Budget> rows)
        => rows.Select(b => new BudgetToolDto(b.Id, b.Name, b.Description, b.IsDefault, b.Year, b.Month));
    private static BudgetToolDto Map(Budget b) => new(b.Id, b.Name, b.Description, b.IsDefault, b.Year, b.Month);
    private static MonthlyBudgetToolDto Map(MonthlyBudget b) => new(b.Id, b.CategoryId, b.IsDefault, b.Year, b.Month, b.PlannedAmount, b.AllowRollover, b.Notes);
}

/// <summary>Named budget metadata input.</summary>
public sealed record BudgetWriteInput([property: Required, StringLength(100)] string Name,
    [property: StringLength(500)] string? Description = null, bool IsDefault = false, int? Year = null, int? Month = null);
/// <summary>Named budget metadata, with allocations paged separately.</summary>
public sealed record BudgetToolDto(int Id, string Name, string? Description, bool IsDefault, int? Year, int? Month);
/// <summary>Category allocation output.</summary>
public sealed record BudgetAllocationToolDto(int Id, int BudgetId, int CategoryId, decimal PlannedAmount, bool AllowRollover, string? Notes);
/// <summary>Default or month-specific envelope assignment output.</summary>
public sealed record MonthlyBudgetToolDto(int Id, int CategoryId, bool IsDefault, int? Year, int? Month, decimal PlannedAmount, bool AllowRollover, string? Notes);
