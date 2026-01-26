using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Categories;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryGroup>> GetCategoryGroupsAsync(string userId, bool includeCategories = false, CancellationToken cancellationToken = default)
    {
        IQueryable<CategoryGroup> query = _context.CategoryGroups
            .Where(cg => cg.UserId == userId && cg.IsActive)
            .OrderBy(cg => cg.SortOrder)
           .ThenBy(cg => cg.Name);

        if (includeCategories)
        {
            query = query.Include(cg => cg.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<Category>> GetCategoriesAsync(string userId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Categories
            .Include(c => c.CategoryGroup)
            .Where(c => c.UserId == userId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.CategoryGroup.SortOrder)
            .ThenBy(c => c.CategoryGroup.Name)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetCategoryByIdAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
    }

    public async Task<CategoryGroup> CreateCategoryGroupAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await _context.CategoryGroups
            .Where(cg => cg.UserId == userId)
            .MaxAsync(cg => (int?)cg.SortOrder, cancellationToken) ?? 0;

        var categoryGroup = new CategoryGroup
        {
            UserId = userId,
            Name = name,
            SortOrder = maxSortOrder + 1
        };

        _context.CategoryGroups.Add(categoryGroup);
        await _context.SaveChangesAsync(cancellationToken);

        return categoryGroup;
    }

    public async Task<Category> CreateCategoryAsync(string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await _context.Categories
            .Where(c => c.UserId == userId && c.CategoryGroupId == categoryGroupId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        var category = new Category
        {
            UserId = userId,
            Name = name,
            CategoryGroupId = categoryGroupId,
            SortOrder = maxSortOrder + 1
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<bool> UpdateCategoryAsync(int categoryId, string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.Name = name;
        category.CategoryGroupId = categoryGroupId;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SeedDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Check if user already has categories
        var hasCategories = await _context.Categories.AnyAsync(c => c.UserId == userId, cancellationToken);
        if (hasCategories)
            return;

        // Create default category groups and categories
        var defaults = new Dictionary<string, string[]>
        {
            { "Income", new[] {  "Salary", "Freelance", "Investments", "Other Income" } },
            { "Housing", new[] { "Rent/Mortgage", "Utilities", "Maintenance", "Insurance" } },
            { "Transportation", new[] { "Gas/Fuel", "Public Transit", "Parking", "Car Payment", "Maintenance" } },
            { "Food", new[] { "Groceries", "Restaurants", "Coffee/Snacks" } },
            { "Shopping", new[] { "Clothing", "Electronics", "Home Goods", "Other Shopping" } },
            { "Entertainment", new[] { "Subscriptions", "Movies/Events", "Hobbies" } },
            { "Health", new[] { "Medical", "Pharmacy", "Fitness", "Insurance" } },
            { "Personal", new[] { "Personal Care", "Education", "Gifts" } },
            { "Miscellaneous", new[] { "Fees/Charges", "Taxes", "Uncategorized" } }
        };

        int groupOrder = 1;
        foreach (var (groupName, categories) in defaults)
        {
            var group = new CategoryGroup
            {
                UserId = userId,
                Name = groupName,
                SortOrder = groupOrder++
            };
            _context.CategoryGroups.Add(group);
            await _context.SaveChangesAsync(cancellationToken);

            int categoryOrder = 1;
            foreach (var categoryName in categories)
            {
                var category = new Category
                {
                    UserId = userId,
                    Name = categoryName,
                    CategoryGroupId = group.Id,
                    SortOrder = categoryOrder++
                };
                _context.Categories.Add(category);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
