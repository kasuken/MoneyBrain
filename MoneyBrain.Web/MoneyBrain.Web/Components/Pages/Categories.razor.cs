using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Resources;
using MoneyBrain.Web.Services;
using MudBlazor;
using System.Security.Claims;

namespace MoneyBrain.Web.Components.Pages;

public partial class Categories
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private IMonthlyBudgetService MonthlyBudgetService { get; set; } = default!;
    [Inject] private IUserSettingsService SettingsService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar SnackbarService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ICurrencyFormattingService CurrencyFormattingService { get; set; } = default!;

    private List<CategoryGroup> _categoryGroups = [];
    private List<MonthlyBudget> _monthlyBudgets = [];
    private Dictionary<int, decimal> _actualSpending = new();
    private bool _isLoading = true;
    private string? _userId;
    private string _viewMode = "categories"; // "categories" or "budgets"

    /// <summary>Groups sorted by display order for use in markup.</summary>
    private IEnumerable<CategoryGroup> OrderedGroups =>
        _categoryGroups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name);

    /// <summary>Active categories within a group, sorted by display order.</summary>
    private static IEnumerable<Category> OrderedCategories(CategoryGroup group) =>
        group.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
    private int _selectedYear = DateTime.UtcNow.Year;
    private int _selectedMonth = DateTime.UtcNow.Month;
    private string _selectedMonthName = string.Empty;
    private bool _showingDefaults = false;
    private UserSettings? _userSettings;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(_userId))
        {
            _userSettings = await SettingsService.GetSettingsAsync(_userId);
            await LoadCategories();
            await LoadBudgets();
        }

        _selectedMonthName = new DateTime(_selectedYear, _selectedMonth, 1).ToString("MMMM");
        _isLoading = false;
    }

    private async Task LoadCategories()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        _categoryGroups = await CategoryService.GetCategoryGroupsAsync(_userId, includeCategories: true);
        
        // Auto-seed default categories if user has none
        if (_categoryGroups.Count == 0)
        {
            await CategoryService.SeedDefaultCategoriesAsync(_userId);
            _categoryGroups = await CategoryService.GetCategoryGroupsAsync(_userId, includeCategories: true);
        }
    }

    private async Task LoadBudgets()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        if (_showingDefaults)
        {
            _monthlyBudgets = await MonthlyBudgetService.GetDefaultBudgetsAsync(_userId);
            // Load actual spending for current month to show overspending indicators
            var now = DateTime.UtcNow;
            _actualSpending = await MonthlyBudgetService.GetAllCategoriesActualSpendingAsync(_userId, now.Year, now.Month);
        }
        else
        {
            _monthlyBudgets = await MonthlyBudgetService.GetEffectiveBudgetsForMonthAsync(_userId, _selectedYear, _selectedMonth);
            _actualSpending = await MonthlyBudgetService.GetAllCategoriesActualSpendingAsync(_userId, _selectedYear, _selectedMonth);
        }
    }

    private async Task ToggleDefaultView()
    {
        _showingDefaults = !_showingDefaults;
        await LoadBudgets();
    }

    private async Task SetViewMode(string mode)
    {
        _viewMode = mode;
        if (mode == "budgets" && _monthlyBudgets.Count == 0)
        {
            await LoadBudgets();
        }
    }

    private async Task PreviousMonth()
    {
        _selectedMonth--;
        if (_selectedMonth < 1)
        {
            _selectedMonth = 12;
            _selectedYear--;
        }
        _selectedMonthName = new DateTime(_selectedYear, _selectedMonth, 1).ToString("MMMM");
        await LoadBudgets();
    }

    private async Task NextMonth()
    {
        _selectedMonth++;
        if (_selectedMonth > 12)
        {
            _selectedMonth = 1;
            _selectedYear++;
        }
        _selectedMonthName = new DateTime(_selectedYear, _selectedMonth, 1).ToString("MMMM");
        await LoadBudgets();
    }

    private async Task CurrentMonth()
    {
        _selectedYear = DateTime.UtcNow.Year;
        _selectedMonth = DateTime.UtcNow.Month;
        _selectedMonthName = new DateTime(_selectedYear, _selectedMonth, 1).ToString("MMMM");
        await LoadBudgets();
    }

    private async Task OpenCreateGroupDialog()
    {
        var parameters = new DialogParameters<CategoryGroupDialog>
        {
            { x => x.UserId, _userId },
            { x => x.IsNew, true }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CategoryGroupDialog>(L["Cat_NewCategoryGroupTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(L["Cat_GroupCreatedSuccess"], Severity.Success);
        }
    }

    private async Task OpenEditGroupDialog(CategoryGroup group)
    {
        var parameters = new DialogParameters<CategoryGroupDialog>
        {
            { x => x.UserId, _userId },
            { x => x.CategoryGroup, group },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CategoryGroupDialog>(L["Cat_EditCategoryGroupTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(L["Cat_GroupUpdatedSuccess"], Severity.Success);
        }
    }

    private async Task DeleteGroup(CategoryGroup group)
    {
        var activeCategoriesCount = group.Categories.Count(c => c.IsActive);
        
        var message = activeCategoriesCount > 0
            ? string.Format(L["Cat_GroupHasActiveCategories"], activeCategoriesCount)
            : L["Cat_ConfirmDeleteGroup"].Value;

        var result = await DialogService.ShowMessageBox(
            L["Cat_DeleteCategoryGroupTitle"],
            message,
            yesText: activeCategoriesCount > 0 ? L["Btn_Ok"] : L["Btn_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            try
            {
                var deleted = await CategoryService.DeleteCategoryGroupAsync(group.Id, _userId);
                if (deleted)
                {
                    await LoadCategories();
                    SnackbarService.Add(L["Cat_GroupDeletedSuccess"], Severity.Success);
                }
                else
                {
                    SnackbarService.Add(L["Cat_GroupDeleteFailed"], Severity.Error);
                }
            }
            catch (InvalidOperationException ex)
            {
                SnackbarService.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task OpenCreateCategoryDialog(int? groupId = null)
    {
        var parameters = new DialogParameters<CategoryDialog>
        {
            { x => x.UserId, _userId },
            { x => x.IsNew, true },
            { x => x.PreselectedGroupId, groupId ?? 0 }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CategoryDialog>(L["Cat_NewCategoryTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(L["Cat_CategoryCreatedSuccess"], Severity.Success);
        }
    }

    private async Task OpenEditCategoryDialog(Category category)
    {
        var parameters = new DialogParameters<CategoryDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Category, category },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CategoryDialog>(L["Cat_EditCategoryTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(L["Cat_CategoryUpdatedSuccess"], Severity.Success);
        }
    }

    private async Task DeleteCategory(Category category)
    {
        var result = await DialogService.ShowMessageBox(
            L["Cat_DeleteCategoryTitle"],
            string.Format(L["Cat_ConfirmDeleteCategory"], category.Name),
            yesText: L["Btn_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            var deleted = await CategoryService.DeleteCategoryAsync(category.Id, _userId);
            if (deleted)
            {
                await LoadCategories();
                SnackbarService.Add(L["Cat_CategoryDeletedSuccess"], Severity.Success);
            }
            else
            {
                SnackbarService.Add(L["Cat_CategoryDeleteFailed"], Severity.Error);
            }
        }
    }

    private async Task OpenRenameCategoryDialog(Category category)
    {
        var parameters = new DialogParameters<CategoryDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Category, category },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CategoryDialog>(L["Cat_RenameCategoryTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(L["Cat_CategoryRenamedSuccess"], Severity.Success);
        }
    }

    private async Task OpenUsageHistoryDialog(Category category)
    {
        var parameters = new DialogParameters<CategoryUsageHistoryDialog>
        {
            { x => x.Category, category },
            { x => x.UserId, _userId }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true };
        await DialogService.ShowAsync<CategoryUsageHistoryDialog>(string.Format(L["Cat_UsageHistoryTitle"], category.Name), parameters, options);
    }

    private async Task OpenMergeCategoryDialog(Category category)
    {
        var parameters = new DialogParameters<MergeCategoryDialog>
        {
            { x => x.SourceCategory, category },
            { x => x.UserId, _userId }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<MergeCategoryDialog>(L["Cat_MergeCategoryTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadCategories();
            SnackbarService.Add(string.Format(L["Cat_CategoryMergedSuccess"], category.Name), Severity.Success);
        }
    }

    private async Task OpenBudgetDialog(Category category, bool isDefault = false)
    {
        var parameters = new DialogParameters<BudgetAssignmentDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Category, category },
            { x => x.Year, _selectedYear },
            { x => x.Month, _selectedMonth },
            { x => x.IsDefaultMode, isDefault }
        };

        var title = isDefault ? L["Cat_SetDefaultBudget"].Value : string.Format(L["Cat_SetBudgetForMonth"], _selectedMonthName, _selectedYear);
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<BudgetAssignmentDialog>(title, parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadBudgets();
            SnackbarService.Add(L["Cat_BudgetUpdatedSuccess"], Severity.Success);
        }
    }
}
