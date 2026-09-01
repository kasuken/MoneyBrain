using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Resources;
using MoneyBrain.Web.Services;
using MudBlazor;
using System.Security.Claims;

namespace MoneyBrain.Web.Components.Pages;

public partial class Accounts
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private IAccountService AccountService { get; set; } = default!;
    [Inject] private IUserSettingsService SettingsService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar SnackbarService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ICurrencyFormattingService CurrencyFormattingService { get; set; } = default!;

    private List<AccountWithBalance> _accounts = [];
    private bool _isLoading = true;
    private string? _userId;
    private string _currencyCode = "USD";
    
    private decimal _totalAssets;
    private decimal _totalLiabilities;
    private decimal _netWorth;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(_userId))
        {
            var settings = await SettingsService.GetSettingsAsync(_userId);
            if (settings != null)
            {
                _currencyCode = settings.CurrencyCode;
            }
            await LoadAccounts();
        }

        _isLoading = false;
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload accounts when navigating back to this page
        if (!_isLoading && !string.IsNullOrWhiteSpace(_userId))
        {
            await LoadAccounts();
            StateHasChanged();
        }
    }

    private async Task LoadAccounts()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        var accounts = await AccountService.GetUserAccountsAsync(_userId);
        _accounts = [];
        
        var now = DateTime.Now;
        var currentYear = now.Year;
        var currentMonth = now.Month;
        
        foreach (var account in accounts)
        {
            var balance = await AccountService.CalculateCurrentBalanceAsync(account.Id, _userId!);
            
            // Get monthly spending summary if account has a limit set
            MonthlySpendingSummary? spendingSummary = null;
            if (account.MonthlySpendingLimit.HasValue)
            {
                spendingSummary = await AccountService.GetMonthlySpendingAsync(
                    account.Id, _userId!, currentYear, currentMonth);
            }
            
            _accounts.Add(new AccountWithBalance(account, balance, spendingSummary));
        }
        
        _totalAssets = _accounts.Where(a => a.Type == AccountType.Asset).Sum(a => a.CurrentBalance);
        _totalLiabilities = _accounts.Where(a => a.Type == AccountType.Liability).Sum(a => Math.Abs(a.CurrentBalance));
        _netWorth = _totalAssets - _totalLiabilities;
    }

    private async Task OpenCreateAccountDialog()
    {
        var parameters = new DialogParameters<AccountDialog>
        {
            { x => x.UserId, _userId },
            { x => x.IsNew, true }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AccountDialog>(L["Accounts_DialogCreateTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadAccounts();
            SnackbarService.Add(L["Accounts_CreatedSuccess"], Severity.Success);
        }
    }

    private async Task OpenEditAccountDialog(MoneyBrain.Web.Domain.Entities.Account account)
    {
        var parameters = new DialogParameters<AccountDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Account, account },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AccountDialog>(L["Accounts_DialogEditTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadAccounts();
            SnackbarService.Add(L["Accounts_UpdatedSuccess"], Severity.Success);
        }
    }

    private async Task DeleteAccount(MoneyBrain.Web.Domain.Entities.Account account)
    {
        var result = await DialogService.ShowMessageBox(
            L["Accounts_DialogDeleteTitle"],
            string.Format(L["Accounts_ConfirmDeleteMessage"], account.Name),
            yesText: L["Common_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            var deleted = await AccountService.DeleteAccountAsync(account.Id, _userId);
            if (deleted)
            {
                await LoadAccounts();
                SnackbarService.Add(L["Accounts_DeletedSuccess"], Severity.Success);
            }
            else
            {
                SnackbarService.Add(L["Accounts_DeleteFailed"], Severity.Error);
            }
        }
    }

    private void ViewBalanceHistory(AccountWithBalance account)
    {
        NavigationManager.NavigateTo($"/balance-history?accountId={account.Id}");
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

    private string GetAccountIconClass(AccountSubType subType) => subType switch
    {
        AccountSubType.Checking or AccountSubType.Savings => "bank",
        AccountSubType.Cash => "cash",
        AccountSubType.CreditCard or AccountSubType.Loan or AccountSubType.Mortgage or AccountSubType.OtherLiability => "credit",
        AccountSubType.Investment => "investment",
        _ => "bank"
    };

    private string GetAccountIcon(AccountSubType subType) => subType switch
    {
        AccountSubType.Checking or AccountSubType.Savings => Icons.Material.Filled.AccountBalance,
        AccountSubType.Cash => Icons.Material.Filled.Money,
        AccountSubType.CreditCard => Icons.Material.Filled.CreditCard,
        AccountSubType.Investment => Icons.Material.Filled.TrendingUp,
        AccountSubType.Loan => Icons.Material.Filled.Receipt,
        AccountSubType.Mortgage => Icons.Material.Filled.Home,
        _ => Icons.Material.Filled.Wallet
    };

    private string FormatAmount(decimal amount) => CurrencyFormattingService.FormatCurrency(amount, _currencyCode);

    private double GetSpendingProgress(MonthlySpendingSummary summary)
    {
        if (summary.PercentUsed.HasValue)
        {
            return Math.Min((double)summary.PercentUsed.Value, 100);
        }
        return 0;
    }

    private MudBlazor.Color GetSpendingColor(MonthlySpendingSummary summary)
    {
        if (summary.IsOverLimit)
            return MudBlazor.Color.Error;
        if (summary.PercentUsed >= 90)
            return MudBlazor.Color.Warning;
        if (summary.PercentUsed >= 75)
            return MudBlazor.Color.Info;
        return MudBlazor.Color.Success;
    }

    private string GetSpendingTooltip(AccountWithBalance account)
    {
        if (account.SpendingSummary == null)
            return "";

        var summary = account.SpendingSummary;
        var isCreditCard = account.SubType == AccountSubType.CreditCard;
        var qualifier = isCreditCard ? "pending (unbilled)" : "posted & pending";
        
        if (summary.IsOverLimit)
        {
            return $"⚠️ Over limit by {FormatAmount(Math.Abs(summary.Remaining ?? 0))}! Includes {qualifier} transactions. Click to view details.";
        }
        if (summary.Remaining.HasValue)
        {
            return $"✓ Remaining: {FormatAmount(summary.Remaining.Value)} ({100 - (summary.PercentUsed ?? 0):N0}% available). Includes {qualifier} transactions. Click to view details.";
        }
        return $"Includes {qualifier} transactions. Click to view this month's transactions.";
    }

    private void ViewMonthlyTransactions(AccountWithBalance account)
    {
        var now = DateTime.Now;
        var startDate = new DateTime(now.Year, now.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        
        // For credit cards, show pending transactions (unbilled charges)
        // For other accounts, show posted transactions
        var status = account.SubType == AccountSubType.CreditCard ? "Pending" : "Posted";
        
        // Navigate to transactions page with filters for this account's expenses this month
        var url = $"/transactions?accountId={account.Id}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&status={status}";
        NavigationManager.NavigateTo(url);
    }

    private record AccountWithBalance(MoneyBrain.Web.Domain.Entities.Account Account, decimal CurrentBalance, MonthlySpendingSummary? SpendingSummary)
    {
        public int Id => Account.Id;
        public string Name => Account.Name;
        public AccountType Type => Account.Type;
        public AccountSubType SubType => Account.SubType;
        public string? Group => Account.Group;
        public decimal? MonthlyLimit => Account.MonthlySpendingLimit;
    }
}
