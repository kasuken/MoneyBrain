using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Application.Transactions;
using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Resources;
using MoneyBrain.Web.Services;
using MudBlazor;
using System.Security.Claims;
using Transaction = MoneyBrain.Web.Domain.Entities.Transaction;
using TransactionStatus = MoneyBrain.Web.Domain.Enums.TransactionStatus;

namespace MoneyBrain.Web.Components.Pages;

public partial class Transactions
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private ITransactionService TransactionService { get; set; } = default!;
    [Inject] private ITransferService TransferService { get; set; } = default!;
    [Inject] private IAccountService AccountService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private IUserSettingsService SettingsService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar SnackbarService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ICurrencyFormattingService CurrencyFormattingService { get; set; } = default!;

    private List<Transaction> _transactions = [];
    private List<MoneyBrain.Web.Domain.Entities.Account> _accounts = [];
    private bool _isLoading = true;
    private string? _userId;
    private string _currencyCode = "USD";
    private TransactionFilter _filter = new();
    private bool _showFilters = false;
    private HashSet<Transaction> _selectedTransactions = [];
    private int _pendingTransactionCount = 0;
    private int _unreconciledTransactionCount = 0;

    // Query string parameters for deep-linking from other pages
    [SupplyParameterFromQuery(Name = "accountId")]
    public int? QueryAccountId { get; set; }

    [SupplyParameterFromQuery(Name = "startDate")]
    public string? QueryStartDate { get; set; }

    [SupplyParameterFromQuery(Name = "endDate")]
    public string? QueryEndDate { get; set; }

    [SupplyParameterFromQuery(Name = "status")]
    public string? QueryStatus { get; set; }

    [SupplyParameterFromQuery(Name = "transactionType")]
    public string? QueryTransactionType { get; set; }

    [SupplyParameterFromQuery(Name = "includeTransfers")]
    public bool? QueryIncludeTransfers { get; set; }

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

            // Apply query string parameters to filter
            ApplyQueryStringParameters();

            _accounts = (await AccountService.GetUserAccountsAsync(_userId)).ToList();
            await LoadTransactions();
            await LoadPendingCount();
            await LoadUnreconciledCount();
        }

        _isLoading = false;
    }

    private void ApplyQueryStringParameters()
    {
        var hasQueryParams = false;

        if (QueryAccountId.HasValue)
        {
            _filter.AccountId = QueryAccountId;
            hasQueryParams = true;
        }

        if (!string.IsNullOrWhiteSpace(QueryStartDate) && DateTime.TryParse(QueryStartDate, out var startDate))
        {
            _filter.StartDate = startDate;
            hasQueryParams = true;
        }

        if (!string.IsNullOrWhiteSpace(QueryEndDate) && DateTime.TryParse(QueryEndDate, out var endDate))
        {
            _filter.EndDate = endDate;
            hasQueryParams = true;
        }

        if (!string.IsNullOrWhiteSpace(QueryStatus) && Enum.TryParse<TransactionStatus>(QueryStatus, true, out var status))
        {
            _filter.Status = status;
            hasQueryParams = true;
        }

        if (!string.IsNullOrWhiteSpace(QueryTransactionType) && Enum.TryParse<TransactionType>(QueryTransactionType, true, out var txType))
        {
            _filter.TransactionType = txType;
            hasQueryParams = true;
        }

        if (QueryIncludeTransfers.HasValue)
        {
            _filter.IncludeTransfers = QueryIncludeTransfers.Value;
            hasQueryParams = true;
        }

        // Show filters panel if any query parameters were applied
        if (hasQueryParams)
        {
            _showFilters = true;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_isLoading && _userId != null)
        {
            await LoadTransactions();
        }
    }

    private async Task LoadTransactions()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        _transactions = await TransactionService.SearchTransactionsAsync(_userId, _filter);
    }

    private async Task LoadPendingCount()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        _pendingTransactionCount = await TransactionService.GetPendingTransactionCountAsync(_userId);
    }

    private async Task LoadUnreconciledCount()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;

        var unreconciledFilter = new TransactionFilter { IsReconciled = false };
        var unreconciledTransactions = await TransactionService.SearchTransactionsAsync(_userId, unreconciledFilter);
        _unreconciledTransactionCount = unreconciledTransactions.Count;
    }

    private async Task ApplyFilters()
    {
        await LoadTransactions();
    }

    private async Task ClearFilters()
    {
        _filter.Clear();
        await LoadTransactions();
    }

    private int GetActiveFilterCount()
    {
        int count = 0;
        
        if (_filter.AccountId.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(_filter.SearchText)) count++;
        if (_filter.StartDate.HasValue) count++;
        if (_filter.EndDate.HasValue) count++;
        if (_filter.MinAmount.HasValue) count++;
        if (_filter.MaxAmount.HasValue) count++;
        if (_filter.TransactionType.HasValue) count++;
        if (_filter.CategoryIds?.Any() == true) count++;
        if (_filter.PayeeIds?.Any() == true) count++;
        if (_filter.Status.HasValue) count++;
        if (_filter.IsCleared.HasValue) count++;
        if (_filter.IsReconciled.HasValue) count++;
        if (!_filter.IncludeTransfers) count++;
        if (_filter.Tags?.Any() == true) count++;
        
        return count;
    }

    private async Task OpenCreateTransactionDialog()
    {
        var parameters = new DialogParameters<TransactionDialog>
        {
            { x => x.UserId, _userId },
            { x => x.IsNew, true },
            { x => x.PreselectedAccountId, _filter.AccountId }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TransactionDialog>(L["Trans_DialogAddTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTransactions();
            SnackbarService.Add(L["Trans_CreatedSuccess"], Severity.Success);
        }
    }

    private async Task OpenCsvImportDialog()
    {
        var parameters = new DialogParameters<TransactionCsvImportDialog>
        {
            { x => x.UserId, _userId }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TransactionCsvImportDialog>(L["Trans_DialogImportTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: int importedCount })
        {
            if (importedCount > 0)
            {
                await LoadTransactions();
                SnackbarService.Add(string.Format(L["Trans_ImportedCount"], importedCount), Severity.Success);
            }
        }
    }

    private async Task OpenEditTransactionDialog(Transaction transaction)
    {
        var parameters = new DialogParameters<TransactionDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Transaction, transaction },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TransactionDialog>(L["Trans_DialogEditTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTransactions();
            SnackbarService.Add(L["Trans_UpdatedSuccess"], Severity.Success);
        }
    }

    private async Task DeleteTransaction(Transaction transaction)
    {
        var result = await DialogService.ShowMessageBox(
            L["Trans_DialogDeleteTitle"],
            L["Trans_ConfirmDeleteMessage"],
            yesText: L["Btn_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            try
            {
                var deleted = await TransactionService.DeleteTransactionAsync(transaction.Id, _userId);
                if (deleted)
                {
                    await LoadTransactions();
                    SnackbarService.Add(L["Trans_DeletedSuccess"], Severity.Success);
                }
                else
                {
                    SnackbarService.Add(L["Trans_DeleteFailed"], Severity.Error);
                }
            }
            catch (InvalidOperationException)
            {
                SnackbarService.Add(L["Trans_CannotDeleteReconciledError"], Severity.Error);
            }
        }
    }

    private string FormatCurrency(decimal amount)
    {
        var sign = amount >= 0 ? "+" : "-";
        return $"{sign}{CurrencyFormattingService.FormatCurrency(Math.Abs(amount), _currencyCode)}";
    }

    private string GetAmountClass(decimal amount) => amount switch
    {
        > 0 => "positive",
        < 0 => "negative",
        _ => "neutral"
    };

    private void ClearSelection()
    {
        _selectedTransactions.Clear();
    }

    private async Task OpenBulkEditDialog()
    {
        if (_selectedTransactions.Count == 0)
            return;

        var selectedIds = _selectedTransactions.Select(t => t.Id).ToList();
        var reconciledCount = _selectedTransactions.Count(t => t.IsReconciled);

        var parameters = new DialogParameters<BulkEditTransactionDialog>
        {
            { x => x.UserId, _userId },
            { x => x.SelectedTransactionIds, selectedIds },
            { x => x.ReconciledCount, reconciledCount }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<BulkEditTransactionDialog>(L["Trans_DialogBulkEditTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: BulkEditResult editResult })
        {
            if (editResult.UpdatedCount > 0)
            {
                await LoadTransactions();
                _selectedTransactions.Clear();
                
                var message = string.Format(L["Trans_UpdatedCount"], editResult.UpdatedCount);
                if (editResult.SkippedTransactionIds.Count > 0)
                {
                    message += " " + string.Format(L["Trans_SkippedCount"], editResult.SkippedTransactionIds.Count, editResult.SkipReason);
                }
                SnackbarService.Add(message, Severity.Success);
            }
            else if (editResult.SkippedTransactionIds.Count > 0)
            {
                SnackbarService.Add(string.Format(L["Trans_AllSkipped"], editResult.SkipReason), Severity.Warning);
            }
        }
    }

    private async Task OpenCreateTransferDialog()
    {
        var parameters = new DialogParameters<TransferDialog>
        {
            { x => x.UserId, _userId },
            { x => x.IsNew, true }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TransferDialog>(L["Trans_DialogCreateTransferTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTransactions();
            SnackbarService.Add(L["Trans_TransferCreatedSuccess"], Severity.Success);
        }
    }

    private async Task OpenEditTransferDialog(Transaction transaction)
    {
        if (!transaction.TransferTransactionId.HasValue)
            return;

        var transfer = await TransferService.GetTransferAsync(_userId!, transaction.Id);
        if (transfer == null)
        {
            SnackbarService.Add(L["Trans_TransferNotFound"], Severity.Error);
            return;
        }

        var parameters = new DialogParameters<TransferDialog>
        {
            { x => x.UserId, _userId },
            { x => x.Transfer, transfer },
            { x => x.IsNew, false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TransferDialog>(L["Trans_DialogEditTransferTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTransactions();
            SnackbarService.Add(L["Trans_TransferUpdatedSuccess"], Severity.Success);
        }
    }

    private async Task DeleteTransfer(Transaction transaction)
    {
        if (!transaction.TransferTransactionId.HasValue)
            return;

        var transfer = await TransferService.GetTransferAsync(_userId!, transaction.Id);
        if (transfer == null)
        {
            SnackbarService.Add(L["Trans_TransferNotFound"], Severity.Error);
            return;
        }

        var result = await DialogService.ShowMessageBox(
            L["Trans_DialogDeleteTransferTitle"],
            string.Format(L["Trans_ConfirmDeleteTransferMessage"], FormatCurrency(transfer.Amount), transfer.FromTransaction.Account.Name, transfer.ToTransaction.Account.Name),
            yesText: L["Btn_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            try
            {
                var deleted = await TransferService.DeleteTransferAsync(_userId, transaction.Id);
                if (deleted)
                {
                    await LoadTransactions();
                    SnackbarService.Add(L["Trans_TransferDeletedSuccess"], Severity.Success);
                }
                else
                {
                    SnackbarService.Add(L["Trans_TransferDeleteFailed"], Severity.Error);
                }
            }
            catch (InvalidOperationException ex)
            {
                SnackbarService.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task ShowPendingTransactions()
    {
        _filter.Clear();
        _filter.Status = TransactionStatus.Pending;
        await LoadTransactions();
    }

    private async Task ShowUnreconciledTransactions()
    {
        _filter.Clear();
        _filter.IsReconciled = false;
        await LoadTransactions();
    }

    private async Task PostAllPendingTransactions()
    {
        var result = await DialogService.ShowMessageBox(
            L["Trans_DialogPostAllTitle"],
            string.Format(L["Trans_ConfirmPostAllMessage"], _pendingTransactionCount),
            yesText: L["Trans_PostAll"],
            cancelText: L["Btn_Cancel"]);

        if (result == true && _userId != null)
        {
            try
            {
                var statusResult = await TransactionService.PostAllPendingTransactionsAsync(_userId);
                if (statusResult.UpdatedCount > 0)
                {
                    await LoadTransactions();
                    await LoadPendingCount();
                    await LoadUnreconciledCount();
                    SnackbarService.Add(string.Format(L["Trans_PostedCount"], statusResult.UpdatedCount), Severity.Success);
                }
                else
                {
                    SnackbarService.Add(L["Trans_NoTransactionsPosted"], Severity.Info);
                }
            }
            catch (Exception ex)
            {
                SnackbarService.Add(string.Format(L["Trans_ErrorPosting"], ex.Message), Severity.Error);
            }
        }
    }

    private async Task ToggleCleared(Transaction transaction)
    {
        try
        {
            var success = await TransactionService.ToggleClearedFlagAsync(_userId!, transaction.Id);
            if (success)
            {
                await LoadTransactions();
                var newState = !transaction.IsCleared ? L["Trans_MarkedCleared"] : L["Trans_MarkedUncleared"];
                SnackbarService.Add(newState, Severity.Success);
            }
            else
            {
                SnackbarService.Add(L["Trans_FailedToUpdate"], Severity.Error);
            }
        }
        catch (InvalidOperationException ex)
        {
            SnackbarService.Add(ex.Message, MudBlazor.Severity.Error);
        }
    }

    private string GetGroupDateHeader(MudBlazor.GroupDefinition<Transaction> groupDef)
    {
        if (groupDef.Grouping?.Key is DateTime date)
        {
            return date.ToString("dddd, MMMM dd, yyyy");
        }
        return groupDef.Grouping?.Key?.ToString() ?? "";
    }
}
