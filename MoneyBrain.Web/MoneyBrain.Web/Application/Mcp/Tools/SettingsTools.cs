using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Settings;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Nonsecret display preferences only; never authentication, infrastructure or licensing settings.</summary>
[McpServerToolType]
public sealed class SettingsTools(McpAccess access, IUserSettingsService settings, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Reads display settings without identity fields.</summary>
    [McpServerTool(Name = "settings_get", ReadOnly = true, OpenWorld = false)]
    [Description("Get your currency, timezone, date format and tips display preferences. No credentials, licensing or security settings are exposed.")]
    public async Task<SettingsToolDto?> Get(CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        var row = await FinanceToolSupport.Run(() => settings.GetSettingsAsync(user, cancellationToken));
        return row is null ? null : Map(row);
    }

    /// <summary>Saves validated display settings.</summary>
    [McpServerTool(Name = "settings_save", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Save supported display currency, system timezone and date format. Completes user display setup. Read settings_get first. No security/licensing settings are writable.")]
    public async Task<SettingsToolDto> Save(string currencyCode, string timeZoneId, string dateFormat = "yyyy-MM-dd", bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        currencyCode = FinanceToolSupport.Text(currencyCode, "Currency code", 3).ToUpperInvariant();
        timeZoneId = FinanceToolSupport.Text(timeZoneId, "Timezone", 100);
        FinanceToolSupport.Check(settings.GetAvailableCurrencies().Any(c => c.Code == currencyCode), "Unsupported currency code.");
        FinanceToolSupport.Check(TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _), "Timezone is not available on this host.");
        FinanceToolSupport.Check(dateFormat is "yyyy-MM-dd" or "MM/dd/yyyy" or "dd/MM/yyyy" or "dd.MM.yyyy", "Unsupported date format.");
        return Map(await FinanceToolSupport.Run(() => settings.SaveSettingsAsync(user, currencyCode, timeZoneId, dateFormat, cancellationToken)));
    }

    /// <summary>Updates the persisted user display preferences, not the stub tip preference API.</summary>
    [McpServerTool(Name = "settings_tips_preferences_save", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set persisted tips display switches. Requires settings setup first. Read settings_get before replacing preferences.")]
    public async Task<MutationResult> SaveTipsPreferences(bool showTipsAndInsights, bool showEducationalTips, bool showSpendingInsights, bool showBehavioralInsights, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        return await FinanceToolSupport.Run(async () =>
        {
            await settings.UpdateTipsPreferencesAsync(user, showTipsAndInsights, showEducationalTips, showSpendingInsights, showBehavioralInsights, cancellationToken);
            return new MutationResult(true);
        });
    }

    /// <summary>Lists safe saved-filter metadata; raw serialized filter data is not exposed.</summary>
    [McpServerTool(Name = "settings_saved_filters_list", ReadOnly = true, OpenWorld = false)]
    [Description("List saved transaction filter metadata only, maximum 100 per page. No supported application service exists for changing saved filters.")]
    public async Task<FinancePage<SavedFilterMetadataToolDto>> SavedFilters(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.SavedTransactionFilters.AsNoTracking().Where(f => f.UserId == user).OrderBy(f => f.Id).Skip(offset).Take(limit + 1)
            .Select(f => new SavedFilterMetadataToolDto(f.Id, f.Name, f.IsDefault, f.CreatedAt, f.UpdatedAt)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    private static SettingsToolDto Map(UserSettings s) => new(s.CurrencyCode, s.TimeZoneId, s.DateFormat, s.SetupCompleted,
        s.ShowTipsAndInsights, s.ShowEducationalTips, s.ShowSpendingInsights, s.ShowBehavioralInsights);
}

/// <summary>Nonsecret user display settings.</summary>
public sealed record SettingsToolDto(string CurrencyCode, string TimeZoneId, string DateFormat, bool SetupCompleted,
    bool ShowTipsAndInsights, bool ShowEducationalTips, bool ShowSpendingInsights, bool ShowBehavioralInsights);
/// <summary>Safe saved filter metadata without arbitrary serialized payload.</summary>
public sealed record SavedFilterMetadataToolDto(int Id, string Name, bool IsDefault, DateTime CreatedAt, DateTime UpdatedAt);
