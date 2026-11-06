using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Owned category/group management with explicit merge previews.</summary>
[McpServerToolType]
public sealed class CategoriesTools(McpAccess access, ICategoryService categories, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists category groups in a bounded page.</summary>
    [McpServerTool(Name = "category_groups_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned category groups, maximum 100 per page.")]
    public async Task<FinancePage<CategoryGroupToolDto>> Groups(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.CategoryGroups.AsNoTracking().Where(g => g.UserId == user).OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
            .Skip(offset).Take(limit + 1).Select(g => new CategoryGroupToolDto(g.Id, g.Name, g.Type.ToString(), g.IsActive, g.SortOrder)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Lists categories without entity graphs.</summary>
    [McpServerTool(Name = "categories_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned categories and group IDs, optional group filter, maximum 100 per page.")]
    public async Task<FinancePage<CategoryToolDto>> List(int? groupId = null, bool includeInactive = false, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (groupId is int group) await Group(db, group, user, cancellationToken);
        var rows = await db.Categories.AsNoTracking().Where(c => c.UserId == user && c.CategoryGroup.UserId == user &&
                (includeInactive || c.IsActive) && (groupId == null || c.CategoryGroupId == groupId))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).Skip(offset).Take(limit + 1)
            .Select(c => new CategoryToolDto(c.Id, c.Name, c.CategoryGroupId, c.IsActive, c.SortOrder)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Creates a group.</summary>
    [McpServerTool(Name = "category_groups_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a category group. Type is Income or Expense. Not retry-safe.")]
    public async Task<CategoryGroupToolDto> CreateGroup(string name, CategoryType type = CategoryType.Expense, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name");
        FinanceToolSupport.Check(Enum.IsDefined(type), "Invalid category type.");
        var g = await FinanceToolSupport.Run(() => categories.CreateCategoryGroupAsync(user, name, type, cancellationToken));
        return new(g.Id, g.Name, g.Type.ToString(), g.IsActive, g.SortOrder);
    }

    /// <summary>Updates a group after validating its type.</summary>
    [McpServerTool(Name = "category_groups_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Rename a group or change its Income/Expense type. Preview category_groups_list first; changing type affects future sign normalization.")]
    public async Task<MutationResult> UpdateGroup(int groupId, string name, CategoryType type, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name");
        FinanceToolSupport.Check(Enum.IsDefined(type), "Invalid category type.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.UpdateCategoryGroupAsync(groupId, user, name, type, cancellationToken)));
    }

    /// <summary>Archives an empty group.</summary>
    [McpServerTool(Name = "category_groups_archive", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Archive a group only when it has no active categories. Preview categories_list with groupId first.")]
    public async Task<MutationResult> ArchiveGroup(int groupId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.DeleteCategoryGroupAsync(groupId, user, cancellationToken)));
    }

    /// <summary>Sets group ordering for a bounded explicit owned selection.</summary>
    [McpServerTool(Name = "category_groups_reorder", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set category group order using 1 to 100 unique owned group IDs. Preview category_groups_list first.")]
    public async Task<MutationResult> ReorderGroups(List<int> orderedGroupIds, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        var ids = FinanceToolSupport.Ids(orderedGroupIds);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        FinanceToolSupport.Check(await db.CategoryGroups.CountAsync(g => g.UserId == user && ids.Contains(g.Id), cancellationToken) == ids.Count, "Category group not found.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.ReorderCategoryGroupsAsync(user, ids, cancellationToken)));
    }

    /// <summary>Sets category ordering within one owned group.</summary>
    [McpServerTool(Name = "categories_reorder", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set order of 1 to 100 unique owned categories in a single group. Preview categories_list with groupId first.")]
    public async Task<MutationResult> Reorder(int groupId, List<int> orderedCategoryIds, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        var ids = FinanceToolSupport.Ids(orderedCategoryIds);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Group(db, groupId, user, cancellationToken);
        FinanceToolSupport.Check(await db.Categories.CountAsync(c => c.UserId == user && c.CategoryGroupId == groupId && ids.Contains(c.Id), cancellationToken) == ids.Count, "Category not found in this group.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.ReorderCategoriesAsync(user, groupId, ids, cancellationToken)));
    }

    /// <summary>Creates a category in an owned group.</summary>
    [McpServerTool(Name = "categories_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a category in an owned group. Not retry-safe.")]
    public async Task<CategoryToolDto> Create(string name, int groupId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Group(db, groupId, user, cancellationToken);
        var c = await FinanceToolSupport.Run(() => categories.CreateCategoryAsync(user, name, groupId, cancellationToken));
        return new(c.Id, c.Name, c.CategoryGroupId, c.IsActive, c.SortOrder);
    }

    /// <summary>Renames or moves a category to an owned group.</summary>
    [McpServerTool(Name = "categories_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Rename/move an owned category. Preview categories_list first. Preserves category ID and history.")]
    public async Task<MutationResult> Update(int categoryId, string name, int groupId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Group(db, groupId, user, cancellationToken);
        await FinanceToolSupport.Category(db, categoryId, user, cancellationToken);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.UpdateCategoryAsync(categoryId, user, name, groupId, cancellationToken)));
    }

    /// <summary>Archives a category without deleting history.</summary>
    [McpServerTool(Name = "categories_archive", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Archive an owned category, retaining history. Preview categories_list first.")]
    public async Task<MutationResult> Archive(int categoryId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.DeleteCategoryAsync(categoryId, user, cancellationToken)));
    }

    /// <summary>Summarizes the financial references affected by a category merge.</summary>
    [McpServerTool(Name = "categories_merge_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview source category references before merging. Reconciled references prevent a merge to preserve locked history.")]
    public async Task<CategoryMergePreviewToolDto> MergePreview(int sourceCategoryId, int targetCategoryId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Preview(db, user, sourceCategoryId, targetCategoryId, cancellationToken);
    }

    /// <summary>Merges only after verifying no reconciled history can be changed.</summary>
    [McpServerTool(Name = "categories_merge", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Merge source into target after categories_merge_preview. Rejects any reconciled references; source is archived. Refresh preview after a timeout.")]
    public async Task<MutationResult> Merge(int sourceCategoryId, int targetCategoryId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var preview = await Preview(db, user, sourceCategoryId, targetCategoryId, cancellationToken);
        FinanceToolSupport.Check(preview.ReconciledReferences == 0, "Merge would change reconciled history and is not allowed.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => categories.MergeCategoriesAsync(sourceCategoryId, targetCategoryId, user, cancellationToken)));
    }

    private static async Task Group(ApplicationDbContext db, int id, string user, CancellationToken ct)
        => FinanceToolSupport.Check(await db.CategoryGroups.AnyAsync(g => g.Id == id && g.UserId == user && g.IsActive, ct), "Active category group not found.");

    private static async Task<CategoryMergePreviewToolDto> Preview(ApplicationDbContext db, string user, int source, int target, CancellationToken ct)
    {
        FinanceToolSupport.Check(source != target, "Source and target must differ.");
        await FinanceToolSupport.Category(db, source, user, ct);
        await FinanceToolSupport.Category(db, target, user, ct);
        var types = await db.Categories.Where(c => c.UserId == user && (c.Id == source || c.Id == target))
            .Select(c => c.CategoryGroup.Type).Distinct().CountAsync(ct);
        FinanceToolSupport.Check(types == 1, "Categories of different types (income/expense) cannot be merged.");
        var transactions = db.Transactions.Where(t => t.UserId == user && t.CategoryId == source);
        var splits = db.TransactionSplits.Where(s => s.Transaction.UserId == user && s.CategoryId == source);
        var locked = await transactions.CountAsync(t => t.IsReconciled, ct) + await splits.CountAsync(s => s.Transaction.IsReconciled, ct) +
            await db.LedgerEntries.CountAsync(e => e.UserId == user && e.CategoryId == source && e.Transaction.IsReconciled, ct);
        return new(source, target, await transactions.CountAsync(ct), await splits.CountAsync(ct), locked);
    }
}

/// <summary>Flat category group metadata.</summary>
public sealed record CategoryGroupToolDto(int Id, string Name, string Type, bool IsActive, int SortOrder);
/// <summary>Flat category metadata.</summary>
public sealed record CategoryToolDto(int Id, string Name, int GroupId, bool IsActive, int SortOrder);
/// <summary>Counts of merge-affected references; not a reservation against concurrent changes.</summary>
public sealed record CategoryMergePreviewToolDto(int SourceCategoryId, int TargetCategoryId, int Transactions, int Splits, int ReconciledReferences);
