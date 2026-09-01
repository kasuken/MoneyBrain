using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Budgets;
using MoneyBrain.Web.Application.Reporting.BudgetComparison;
using MoneyBrain.Web.Application.Reporting.Cashflow;
using MoneyBrain.Web.Application.Reporting.NetWorth;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Application.Tips;
using MoneyBrain.Web.Application.Tips.DTOs;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Resources;
using MoneyBrain.Web.Services;
using MudBlazor;

namespace MoneyBrain.Web.Components.Pages;

public partial class Dashboard
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private IAccountService AccountService { get; set; } = default!;
    [Inject] private IUserSettingsService SettingsService { get; set; } = default!;
    [Inject] private ICashflowService CashflowService { get; set; } = default!;
    [Inject] private INetWorthService NetWorthService { get; set; } = default!;
    [Inject] private IBudgetComparisonService BudgetComparisonService { get; set; } = default!;
    [Inject] private IBudgetService BudgetService { get; set; } = default!;
    [Inject] private IEducationalTipService EducationalTipService { get; set; } = default!;
    [Inject] private ITipPreferenceService TipPreferenceService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ICurrencyFormattingService CurrencyFormattingService { get; set; } = default!;

    private bool _loading = true;
    private string? _userId;
    private string _currencyCode = "USD";
    private UserSettings? _userSettings;
    
    private DateTime _periodStart;
    private DateTime _periodEnd;
    
    private decimal _totalBalance;
    private decimal _periodChange;
    private decimal _totalExpenses;
    private decimal _totalIncome;
    
    private decimal _pendingIncome;
    private decimal _pendingExpenses;
    private decimal _pendingPeriodChange;
    
    private decimal _combinedIncome;
    private decimal _combinedExpenses;
    private decimal _combinedPeriodChange;
    
    // Chart data
    private List<ChartSeries> _incomeExpenseChartSeries = [];
    private string[] _incomeExpenseChartLabels = [];
    private double[] _categoryPieData = [];
    private string[] _categoryPieLabels = [];
    
    // Tips & Insights
    private List<EducationalTipDto> _educationalTips = [];
    
    // Trend chart data
    private List<ChartSeries> _netWorthTrendSeries = [];
    private string[] _netWorthTrendLabels = [];
    private List<ChartSeries> _cashflowTrendSeries = [];
    private string[] _cashflowTrendLabels = [];
    private bool _hasTrendData = false;
    
    // Budget data
    private MonthlyBudgetComparisonDto? _budgetComparison;
    private List<Budget> _activeBudgets = [];

    protected override async Task OnInitializedAsync()
    {
        // Set period to current month
        var today = DateTime.Today;
        _periodStart = new DateTime(today.Year, today.Month, 1);
        _periodEnd = _periodStart.AddMonths(1).AddDays(-1);
        
        await LoadDataAsync();
        _loading = false;
    }

    private async Task LoadDataAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(_userId)) return;
        
        // Load user settings
        _userSettings = await SettingsService.GetSettingsAsync(_userId);
        if (_userSettings != null)
        {
            _currencyCode = _userSettings.CurrencyCode;
        }
        
        // Load educational tips if enabled
        if (_userSettings?.ShowTipsAndInsights == true && _userSettings.ShowEducationalTips)
        {
            var allTips = await EducationalTipService.GetActiveTipsAsync(_userId);
            // Randomize and take 2 tips
            _educationalTips = allTips.OrderBy(_ => Guid.NewGuid()).Take(2).ToList();
        }
        
        // Calculate net worth for total balance
        var netWorth = await NetWorthService.GetNetWorthSnapshotAsync(_userId, DateTime.Today);
        _totalBalance = netWorth.NetWorth;
        
        // Load cashflow data for the selected period
        var cashflow = await CashflowService.GetMonthCashflowAsync(_userId, _periodStart.Year, _periodStart.Month);
        _totalIncome = cashflow.TotalIncome;
        _totalExpenses = cashflow.TotalExpenses;
        _periodChange = cashflow.NetCashflow;
        
        _pendingIncome = cashflow.PendingIncome;
        _pendingExpenses = cashflow.PendingExpenses;
        _pendingPeriodChange = cashflow.PendingNetCashflow;
        
        // Calculate combined totals (posted + pending)
        _combinedExpenses = _totalExpenses + _pendingExpenses;
        _combinedIncome = _totalIncome + _pendingIncome;
        _combinedPeriodChange = _periodChange + _pendingPeriodChange;
        
        // Prepare chart data
        PrepareChartData(cashflow);
        
        // Load budget data for the selected period
        await LoadBudgetDataAsync();
        
        // Load trend data (last 6 months)
        await LoadTrendDataAsync();
    }
    
    private async Task LoadBudgetDataAsync()
    {
        if (string.IsNullOrEmpty(_userId)) return;
        
        try
        {
            _budgetComparison = await BudgetComparisonService.GetMonthlyBudgetComparisonAsync(
                _userId, 
                _periodStart.Year, 
                _periodStart.Month);
                
            // Load active budgets for the current period
            var periodBudgets = await BudgetService.GetBudgetsForPeriodAsync(_userId, _periodStart.Year, _periodStart.Month);
            var defaultBudgets = await BudgetService.GetDefaultBudgetsAsync(_userId);
            
            // Use period-specific budgets if available, otherwise default budgets
            _activeBudgets = periodBudgets.Any() ? periodBudgets : defaultBudgets;
        }
        catch
        {
            // Budget comparison not available - this is okay
            _budgetComparison = null;
            _activeBudgets = [];
        }
    }
    
    private async Task LoadTrendDataAsync()
    {
        if (string.IsNullOrEmpty(_userId)) return;
        
        var endDate = DateTime.Today;
        var startDate = endDate.AddMonths(-5).AddDays(1 - endDate.Day); // Start of month, 6 months ago
        
        // Load cashflow trend
        var cashflowData = await CashflowService.GetMonthlyCashflowAsync(_userId, startDate, endDate);
        
        if (cashflowData.Any())
        {
            _hasTrendData = true;
            
            // Prepare cashflow trend chart
            var orderedCashflow = cashflowData.OrderBy(c => c.Year).ThenBy(c => c.Month).ToList();
            _cashflowTrendLabels = orderedCashflow.Select(c => new DateTime(c.Year, c.Month, 1).ToString("MMM")).ToArray();
            _cashflowTrendSeries = new List<ChartSeries>
            {
                new ChartSeries { Name = L["Dashboard_Income"], Data = orderedCashflow.Select(c => (double)c.TotalIncome).ToArray() },
                new ChartSeries { Name = L["Dashboard_Expenses"], Data = orderedCashflow.Select(c => (double)c.TotalExpenses).ToArray() }
            };
        }
        
        // Load net worth trend
        var netWorthHistory = await NetWorthService.GetNetWorthHistoryAsync(_userId, startDate, endDate, intervalDays: 30);
        
        if (netWorthHistory.Snapshots.Any())
        {
            _hasTrendData = true;
            
            _netWorthTrendLabels = netWorthHistory.Snapshots.Select(s => s.Date.ToString("MMM")).ToArray();
            _netWorthTrendSeries = new List<ChartSeries>
            {
                new ChartSeries { Name = L["Nav_NetWorth"], Data = netWorthHistory.Snapshots.Select(s => (double)s.NetWorth).ToArray() }
            };
        }
    }

    private async Task PreviousMonth()
    {
        _periodStart = _periodStart.AddMonths(-1);
        _periodEnd = _periodStart.AddMonths(1).AddDays(-1);
        await LoadDataAsync();
    }

    private async Task NextMonth()
    {
        if (_periodEnd >= DateTime.Today) return;
        _periodStart = _periodStart.AddMonths(1);
        _periodEnd = _periodStart.AddMonths(1).AddDays(-1);
        if (_periodEnd > DateTime.Today) _periodEnd = DateTime.Today;
        await LoadDataAsync();
    }

    private string FormatCurrency(decimal amount)
    {
        var sign = amount >= 0 ? "+" : "-";
        return $"{sign}{CurrencyFormattingService.FormatCurrency(Math.Abs(amount), _currencyCode)}";
    }

    private string GetBalanceClass(decimal amount) => amount switch
    {
        > 0 => "positive",
        < 0 => "negative",
        _ => "neutral"
    };

    private void PrepareChartData(MonthlyCashflowDto cashflow)
    {
        // Prepare income vs expenses bar chart with confirmed and pending series
        // "Dashboard_Amount" is appropriate for this single-category bar chart that shows all 4 series together for comparison
        _incomeExpenseChartLabels = new[] { L["Dashboard_Amount"].Value };
        _incomeExpenseChartSeries = new List<ChartSeries>
        {
            new ChartSeries 
            { 
                Name = FormatChartLabel(L["Dashboard_Income"].Value, isPending: false),
                Data = new double[] { (double)cashflow.TotalIncome } 
            },
            new ChartSeries 
            { 
                Name = FormatChartLabel(L["Dashboard_Income"].Value, isPending: true),
                Data = new double[] { (double)_pendingIncome } 
            },
            new ChartSeries 
            { 
                Name = FormatChartLabel(L["Dashboard_Expenses"].Value, isPending: false),
                Data = new double[] { (double)cashflow.TotalExpenses } 
            },
            new ChartSeries 
            { 
                Name = FormatChartLabel(L["Dashboard_Expenses"].Value, isPending: true),
                Data = new double[] { (double)_pendingExpenses } 
            }
        };
        
        // Prepare expense category pie chart (top 5 categories)
        var topCategories = cashflow.ExpensesByCategory
            .OrderByDescending(c => c.Amount)
            .Take(5)
            .ToList();
        
        if (topCategories.Any())
        {
            _categoryPieData = topCategories.Select(c => (double)c.Amount).ToArray();
            _categoryPieLabels = topCategories.Select(c => c.CategoryName).ToArray();
        }
        else
        {
            _categoryPieData = new double[] { 1 };
            _categoryPieLabels = new[] { L["Dashboard_NoExpenses"].Value };
        }
    }
    
    private string FormatChartLabel(string label, bool isPending)
    {
        var suffix = isPending ? L["Dashboard_Pending"].Value : L["Dashboard_Confirmed"].Value;
        return $"{label} ({suffix})";
    }
    
    private string GetBudgetHealthClass(decimal percentageUsed) => percentageUsed switch
    {
        <= 75 => "income",
        <= 90 => "info",
        <= 100 => "",
        _ => "expense"
    };
    
    private string GetCategoryIcon(string category) => category switch
    {
        "Saving" => Icons.Material.Filled.Savings,
        "Spending" => Icons.Material.Filled.ShoppingCart,
        "Investing" => Icons.Material.Filled.TrendingUp,
        "Budgeting" => Icons.Material.Filled.AccountBalanceWallet,
        "General" => Icons.Material.Filled.Lightbulb,
        _ => Icons.Material.Filled.Lightbulb
    };
}
