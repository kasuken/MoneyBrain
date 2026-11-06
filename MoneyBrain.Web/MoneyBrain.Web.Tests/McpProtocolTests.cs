using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Application.Mcp;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Exercises tool dispatch using real OAuth-issued bearers, real services and SQLite persistence.</summary>
[TestClass]
public sealed class McpProtocolTests
{
    private MoneyBrainFactory factory = null!;
    private HttpClient client = null!;
    private int ownedAccountId;
    private int foreignAccountId;
    private int ownedTransactionId;
    private int foreignTransactionId;

    /// <summary>Creates an isolated application with legitimate OAuth login and owned/foreign data.</summary>
    [TestInitialize]
    public async Task InitializeAsync()
    {
        factory = new MoneyBrainFactory(enableWrites: true);
        await OAuthFlow.SeedAsync(factory, allowWrites: true);
        client = factory.CreateHttpsClient();
        var token = await OAuthFlow.IssueTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new ApplicationUser { Id = FinanceDatabase.Other, UserName = "other@example.test" });
        var owned = new Account
        {
            UserId = FinanceDatabase.Owner, Name = "Visible account", Type = AccountType.Asset,
            SubType = AccountSubType.Checking
        };
        var foreign = new Account
        {
            UserId = FinanceDatabase.Other, Name = "Foreign secret account", Type = AccountType.Asset,
            SubType = AccountSubType.Checking
        };
        var transaction = new Transaction
        {
            UserId = FinanceDatabase.Owner, Account = owned, Date = new DateTime(2026, 1, 15),
            Amount = -20m, Memo = "Visible memo", IsReconciled = true
        };
        var foreignTransaction = new Transaction
        {
            UserId = FinanceDatabase.Other, Account = foreign, Date = new DateTime(2026, 1, 15),
            Amount = -999m, Memo = "Foreign secret memo"
        };
        db.AddRange(owned, foreign, transaction, foreignTransaction);
        await db.SaveChangesAsync();
        ownedAccountId = owned.Id;
        foreignAccountId = foreign.Id;
        ownedTransactionId = transaction.Id;
        foreignTransactionId = foreignTransaction.Id;
    }

    /// <summary>Disposes the HTTP client, application and in-memory relational database.</summary>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        client?.Dispose();
        if (factory is not null)
            await factory.DisposeAsync();
    }

    /// <summary>The authenticated tool catalog supports actual calls and domain resources.</summary>
    [TestMethod]
    public async Task Authenticated_client_lists_tools_and_reads_domain_resource()
    {
        using var response = await McpHttpTests.SendAsync(client, "tools/list");
        using var rpc = await McpHttpTests.ReadRpcAsync(response);
        var names = rpc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()).ToArray();
        CollectionAssert.IsSubsetOf(new[]
        {
            "accounts_list", "transactions_get", "report_cashflow", "query_execute", "saved_query_execute"
        }, names);

        using var read = await McpHttpTests.SendAsync(client, "resources/read",
            new { uri = "moneybrain://guidance/domain" });
        using var resource = await McpHttpTests.ReadRpcAsync(read);
        var contents = resource.RootElement.GetProperty("result").GetProperty("contents");
        Assert.IsTrue(contents.GetArrayLength() > 0);
        StringAssert.Contains(contents[0].GetProperty("text").GetString() ?? "", "Reconciled");
    }

    /// <summary>Real protocol reads never disclose another user's account or transaction.</summary>
    [TestMethod]
    public async Task Read_tools_return_owned_data_and_deny_foreign_identifiers()
    {
        using var list = await CallAsync("accounts_list", new { });
        var text = list.RootElement.ToString();
        StringAssert.Contains(text, "Visible account");
        Assert.IsFalse(text.Contains("Foreign secret", StringComparison.Ordinal));
        using var owned = await CallAsync("transactions_get", new { transactionId = ownedTransactionId });
        StringAssert.Contains(owned.RootElement.ToString(), "Visible memo");
        using var foreignAccount = await CallAsync("accounts_get", new { accountId = foreignAccountId });
        AssertToolError(foreignAccount);
        using var foreignTransaction = await CallAsync("transactions_get", new { transactionId = foreignTransactionId });
        AssertToolError(foreignTransaction);
        Assert.IsFalse(foreignTransaction.RootElement.ToString().Contains("Foreign secret", StringComparison.Ordinal));
    }

    /// <summary>A read-only bearer cannot mutate data even when the instance permits writes.</summary>
    [TestMethod]
    public async Task Read_scope_cannot_write_even_with_confirmation()
    {
        using var result = await CallAsync("accounts_deactivate", new { accountId = ownedAccountId, confirmed = true });
        AssertToolError(result);

        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsTrue((await db.Accounts.SingleAsync(account => account.Id == ownedAccountId)).IsActive);
    }

    /// <summary>Write scope is necessary but does not replace explicit confirmation for each mutation.</summary>
    [TestMethod]
    public async Task Write_scope_without_confirmation_cannot_mutate()
    {
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var result = await CallAsync("accounts_deactivate", new { accountId = ownedAccountId, confirmed = false });
        AssertToolError(result);

        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsTrue((await db.Accounts.SingleAsync(account => account.Id == ownedAccountId)).IsActive);
    }

    /// <summary>A confirmed, properly scoped mutation affects only the authenticated user's resource.</summary>
    [TestMethod]
    public async Task Confirmed_write_with_write_scope_persists_only_owned_change()
    {
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var denied = await CallAsync("accounts_deactivate", new { accountId = foreignAccountId, confirmed = true });
        using var changed = await CallAsync("accounts_deactivate", new { accountId = ownedAccountId, confirmed = true });
        Assert.IsFalse(changed.RootElement.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean());

        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsFalse((await db.Accounts.SingleAsync(account => account.Id == ownedAccountId)).IsActive);
        Assert.IsTrue((await db.Accounts.SingleAsync(account => account.Id == foreignAccountId)).IsActive);
    }

    /// <summary>Reconciliation remains an immutable boundary even with full OAuth write permission.</summary>
    [TestMethod]
    public async Task Reconciled_transaction_delete_is_denied_over_protocol()
    {
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var result = await CallAsync("transactions_delete", new { transactionId = ownedTransactionId, confirmed = true });
        AssertToolError(result);

        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsTrue(await db.Transactions.AnyAsync(transaction => transaction.Id == ownedTransactionId && transaction.IsReconciled));
    }

    /// <summary>Invalid report ranges fail through public tool methods rather than metadata-only checks.</summary>
    [TestMethod]
    [DataRow("2026-02-01", "2026-01-01")]
    [DataRow("2024-01-01", "2026-01-01")]
    [DataRow("1899-12-31", "1900-01-01")]
    public async Task Cashflow_rejects_invalid_date_ranges(string startDate, string endDate)
    {
        using var result = await CallAsync("report_cashflow", new { startDate, endDate });
        AssertToolError(result);
    }

    /// <summary>Public net-worth tool enforces the maximum snapshot count and positive interval.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(367)]
    [DataRow(1)]
    public async Task Net_worth_rejects_invalid_or_unbounded_snapshot_intervals(int intervalDays)
    {
        using var result = await CallAsync("report_net_worth", new
        {
            startDate = "2026-01-01", endDate = "2026-12-31", intervalDays
        });
        AssertToolError(result);
    }

    /// <summary>Saved-query list pagination enforces both page and page-size limits.</summary>
    [TestMethod]
    [DataRow(0, 50)]
    [DataRow(10001, 50)]
    [DataRow(1, 0)]
    [DataRow(1, 101)]
    public async Task Saved_query_list_rejects_invalid_paging(int page, int pageSize)
    {
        using var result = await CallAsync("saved_queries_list", new { page, pageSize });
        AssertToolError(result);
    }

    /// <summary>Only known typed filter properties/operators can reach the dynamic query engine.</summary>
    [TestMethod]
    [DataRow("User.PasswordHash", PropertyDataType.String, FilterOperator.Equals, "password")]
    [DataRow("Amount", PropertyDataType.String, FilterOperator.Equals, "10")]
    [DataRow("Amount", PropertyDataType.Decimal, FilterOperator.InList, "10,20")]
    [DataRow("Date", PropertyDataType.DateTime, FilterOperator.GreaterThan, "not-a-date")]
    public async Task Query_execution_rejects_unallowlisted_or_invalid_typed_filters(
        string property, PropertyDataType dataType, FilterOperator filterOperator, string value)
    {
        var definition = new QueryDefinition
        {
            Filters = [new FilterCondition { Property = property, DataType = dataType, Operator = filterOperator, Value = value }]
        };
        using var result = await CallAsync("query_execute", new { definition });
        AssertToolError(result);
    }

    /// <summary>Aggregation over unsupported entity types is rejected instead of silently ignoring it.</summary>
    [TestMethod]
    public async Task Nontransaction_aggregation_is_rejected()
    {
        using var result = await CallAsync("query_execute", new
        {
            definition = new QueryDefinition
            {
                TargetEntity = QueryTargetEntity.Account,
                Aggregation = new AggregationConfig { Property = "Amount", Function = AggregationFunction.Sum }
            }
        });
        AssertToolError(result);
    }

    /// <summary>Stored definitions are not trusted as a way to bypass the filter allowlist.</summary>
    [TestMethod]
    public async Task Saved_query_execution_revalidates_unsafe_stored_definition()
    {
        int queryId;
        using (var scope = factory.Services.CreateScope())
        {
            var explorer = scope.ServiceProvider.GetRequiredService<IInsightExplorerService>();
            var saved = await explorer.SaveQueryAsync(FinanceDatabase.Owner, "Legacy unsafe query", null,
                new QueryDefinition
                {
                    Filters = [new FilterCondition { Property = "User.PasswordHash", Value = "password" }]
                });
            queryId = saved.Id;
        }

        using var result = await CallAsync("saved_query_execute", new { queryId });

        AssertToolError(result);
    }

    /// <summary>Actual query dispatch executes DateTime range filtering and hides foreign rows.</summary>
    [TestMethod]
    public async Task Query_execution_applies_date_filter_and_owner_isolation()
    {
        using var result = await CallAsync("query_execute", new
        {
            definition = new QueryDefinition
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Property = "Date", DataType = PropertyDataType.DateTime, Operator = FilterOperator.Between,
                        Value = "2026-01-01", SecondValue = "2026-01-31"
                    }
                ]
            }
        });

        var text = result.RootElement.ToString();
        StringAssert.Contains(text, "Visible memo");
        Assert.IsFalse(text.Contains("Foreign secret", StringComparison.Ordinal));
    }

    /// <summary>The global visibility switch suppresses data for every insight endpoint.</summary>
    [TestMethod]
    [DataRow("insight_spending")]
    [DataRow("insight_budget")]
    [DataRow("insight_net_worth")]
    [DataRow("insight_behavior")]
    [DataRow("educational_tips_list")]
    public async Task Global_insight_opt_out_returns_disabled_without_data(string name)
    {
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserSettings.Add(new UserSettings
            {
                UserId = FinanceDatabase.Owner, CurrencyCode = "USD", TimeZoneId = "UTC",
                ShowTipsAndInsights = false
            });
            await db.SaveChangesAsync();
        }

        using var result = await CallAsync(name, new
        {
            year = 2026, month = 1, asOfDate = "2026-01-31",
            startDate = "2026-01-01", endDate = "2026-01-31"
        });
        AssertDisabledInsight(result);
    }

    /// <summary>Individual user settings remain effective while the global switch is enabled.</summary>
    [TestMethod]
    [DataRow("insight_spending")]
    [DataRow("insight_behavior")]
    [DataRow("educational_tips_list")]
    public async Task Individual_insight_opt_out_returns_disabled_without_data(string name)
    {
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserSettings.Add(new UserSettings
            {
                UserId = FinanceDatabase.Owner, CurrencyCode = "USD", TimeZoneId = "UTC",
                ShowTipsAndInsights = true, ShowSpendingInsights = false,
                ShowBehavioralInsights = false, ShowEducationalTips = false
            });
            await db.SaveChangesAsync();
        }

        using var result = await CallAsync(name, new
        {
            year = 2026, month = 1, startDate = "2026-01-01", endDate = "2026-01-31"
        });
        AssertDisabledInsight(result);
    }

    /// <summary>Valid pagination boundaries are accepted by the real query dispatcher.</summary>
    [TestMethod]
    public async Task Saved_query_paging_accepts_upper_bounds()
    {
        using var result = await CallAsync("saved_queries_list", new { page = 10000, pageSize = 100 });

        Assert.IsFalse(result.RootElement.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean());
    }

    private static void AssertDisabledInsight(JsonDocument document)
    {
        var result = document.RootElement.GetProperty("result");
        Assert.IsFalse(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString() ??
                   throw new AssertFailedException("Insight returned no text payload.");
        using var payload = JsonDocument.Parse(text);
        var properties = payload.RootElement.EnumerateObject().ToDictionary(property => property.Name,
            property => property.Value, StringComparer.OrdinalIgnoreCase);
        Assert.IsFalse(properties["Enabled"].GetBoolean());
        Assert.IsTrue(!properties.TryGetValue("Data", out var data) || data.ValueKind == JsonValueKind.Null,
            "Disabled insights must not disclose a data payload.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(properties["Disclaimer"].GetString()));
    }

    /// <summary>CSV preview is non-mutating and commit tokens are bound to owner and single use.</summary>
    [TestMethod]
    public async Task Csv_preview_commit_is_owner_bound_and_single_use()
    {
        const string memo = "CSV integration memo";
        using var preview = await CallAsync("csv_import_preview", new
        {
            csvContent = $"Date,Amount,Memo\n2026-01-20,-12.50,{memo}\n",
            mapping = new { accountId = ownedAccountId, dateColumn = 0, amountColumn = 1, memoColumn = 2 }
        });
        using var payload = ToolPayload(preview);
        var token = payload.RootElement.GetProperty("previewToken").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(token));
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.AreEqual(0, await db.Transactions.CountAsync(transaction => transaction.Memo == memo));
        }
        using var other = factory.CreateHttpsClient();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(FinanceDatabase.Other);
            Assert.IsNotNull(user);
            Assert.IsTrue((await users.SetEmailAsync(user, "other@example.test")).Succeeded);
            Assert.IsTrue((await users.AddPasswordAsync(user, OAuthFlow.Password)).Succeeded);
        }
        await OAuthFlow.LoginAsync(other, "other@example.test");
        var otherCode = await OAuthFlow.AuthorizeAsync(other, $"{McpAccess.ReadScope} {McpAccess.WriteScope}");
        using var otherExchange = await OAuthFlow.ExchangeAsync(other, otherCode);
        Assert.AreEqual(HttpStatusCode.OK, otherExchange.StatusCode);
        using var otherPayload = JsonDocument.Parse(await otherExchange.Content.ReadAsStringAsync());
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            otherPayload.RootElement.GetProperty("access_token").GetString());
        using var foreignResponse = await McpHttpTests.SendAsync(other, "tools/call",
            new { name = "csv_import_commit", arguments = new { previewToken = token, confirmed = true } });
        using var foreign = await McpHttpTests.ReadRpcAsync(foreignResponse);
        AssertToolError(foreign);
        var ownerToken = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var committed = await CallAsync("csv_import_commit", new { previewToken = token, confirmed = true });
        using var commitPayload = ToolPayload(committed);
        Assert.AreEqual(1, commitPayload.RootElement.GetProperty("importedCount").GetInt32());
        using var replay = await CallAsync("csv_import_commit", new { previewToken = token, confirmed = true });
        AssertToolError(replay);
        using var verification = factory.Services.CreateScope();
        await using var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.AreEqual(1, await context.Transactions.CountAsync(transaction => transaction.Memo == memo));
        Assert.AreEqual(0, await context.Transactions.CountAsync(transaction =>
            transaction.UserId == FinanceDatabase.Other && transaction.Memo == memo));
    }

    /// <summary>Resource and prompt catalogs advertise retrievable, validated shapes rather than metadata-only stubs.</summary>
    [TestMethod]
    public async Task Resource_and_prompt_discovery_support_actual_retrieval_and_argument_validation()
    {
        using var resourcesResponse = await McpHttpTests.SendAsync(client, "resources/list");
        using var resources = await McpHttpTests.ReadRpcAsync(resourcesResponse);
        var uris = resources.RootElement.GetProperty("result").GetProperty("resources").EnumerateArray()
            .Select(resource => resource.GetProperty("uri").GetString()).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "moneybrain://guidance/domain", "moneybrain://context/preferences" }, uris);
        using var preferencesResponse = await McpHttpTests.SendAsync(client, "resources/read",
            new { uri = "moneybrain://context/preferences" });
        using var preferences = await McpHttpTests.ReadRpcAsync(preferencesResponse);
        Assert.IsTrue(preferences.RootElement.GetProperty("result").GetProperty("contents").GetArrayLength() > 0);
        using var promptsResponse = await McpHttpTests.SendAsync(client, "prompts/list");
        using var prompts = await McpHttpTests.ReadRpcAsync(promptsResponse);
        var names = prompts.RootElement.GetProperty("result").GetProperty("prompts").EnumerateArray()
            .Select(prompt => prompt.GetProperty("name").GetString()).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "selected_period_summary", "budget_variance_review", "reconciliation_preparation" }, names);
        using var promptResponse = await McpHttpTests.SendAsync(client, "prompts/get", new
        {
            name = "selected_period_summary",
            arguments = new { fromDate = "2026-01-01", toDate = "2026-01-31" }
        });
        using var prompt = await McpHttpTests.ReadRpcAsync(promptResponse);
        Assert.IsTrue(prompt.RootElement.GetProperty("result").GetProperty("messages").GetArrayLength() > 0);
        using var invalid = await McpHttpTests.SendAsync(client, "prompts/get", new
        {
            name = "selected_period_summary",
            arguments = new { fromDate = "not-a-date", toDate = "2026-01-31" }
        });
        var invalidText = await invalid.Content.ReadAsStringAsync();
        StringAssert.Contains(invalidText, "\"error\"");
        Assert.IsFalse(invalidText.Contains("\"messages\"", StringComparison.Ordinal));
    }

    /// <summary>Transport rejects an oversized actual HTTP request before deserializing tool arguments.</summary>
    [TestMethod]
    public async Task Oversized_HTTP_request_is_rejected()
    {
        using var response = await McpHttpTests.SendAsync(client, "tools/call",
            new { name = "transactions_get", arguments = new { padding = new string('x', 1024 * 1024 + 1) } });

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    /// <summary>Response filtering refuses a single oversized result without returning its financial payload.</summary>
    [TestMethod]
    public async Task Oversized_tool_result_is_replaced_with_safe_error()
    {
        const string secret = "PRIVATE_FINANCIAL_RESULT_MARKER";
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var transaction = await db.Transactions.SingleAsync(item => item.Id == ownedTransactionId);
            transaction.Memo = secret + new string('x', 1024 * 1024 + 1);
            await db.SaveChangesAsync();
        }

        using var response = await CallAsync("transactions_get", new { transactionId = ownedTransactionId });

        AssertToolError(response);
        Assert.IsFalse(response.RootElement.ToString().Contains(secret, StringComparison.Ordinal));
    }

    /// <summary>Instance write disablement remains authoritative even for an already issued write-scoped token.</summary>
    [TestMethod]
    public async Task Disabled_instance_writes_reject_existing_write_scope_bearer()
    {
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        factory.Services.GetRequiredService<IOptions<McpSettings>>().Value.EnableWrites = false;

        using var result = await CallAsync("accounts_deactivate", new { accountId = ownedAccountId, confirmed = true });

        AssertToolError(result);
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsTrue((await db.Accounts.SingleAsync(account => account.Id == ownedAccountId)).IsActive);
    }

    /// <summary>Tool failures and captured application logs do not echo private financial rows or bearer credentials.</summary>
    [TestMethod]
    public async Task Denied_read_error_and_application_logs_do_not_disclose_secrets_or_financial_payloads()
    {
        var bearer = client.DefaultRequestHeaders.Authorization?.Parameter ??
                     throw new AssertFailedException("No OAuth bearer was issued.");

        using var denied = await CallAsync("transactions_get", new { transactionId = foreignTransactionId });

        AssertToolError(denied);
        var output = denied.RootElement + string.Join('\n', factory.Logs.Messages);
        Assert.IsFalse(output.Contains(bearer, StringComparison.Ordinal));
        Assert.IsFalse(output.Contains(OAuthFlow.Password, StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("Foreign secret memo", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("Visible memo", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("PasswordHash", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("StackTrace", StringComparison.Ordinal));
    }

    /// <summary>Both merge preview and mutation reject income/expense reinterpretation over the real protocol.</summary>
    [TestMethod]
    [DataRow("categories_merge_preview")]
    [DataRow("categories_merge")]
    public async Task Cross_type_category_merge_is_denied_over_protocol(string name)
    {
        var expense = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Expense",
            CategoryGroup = new CategoryGroup { UserId = FinanceDatabase.Owner, Name = "Expense group", Type = CategoryType.Expense }
        };
        var income = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Income",
            CategoryGroup = new CategoryGroup { UserId = FinanceDatabase.Owner, Name = "Income group", Type = CategoryType.Income }
        };
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Categories.AddRange(expense, income);
            await db.SaveChangesAsync();
        }
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var result = await CallAsync(name, new
        {
            sourceCategoryId = expense.Id, targetCategoryId = income.Id, confirmed = true
        });

        AssertToolError(result);
        using var verification = factory.Services.CreateScope();
        await using var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.IsTrue((await context.Categories.SingleAsync(category => category.Id == expense.Id)).IsActive);
    }

    /// <summary>Protocol split creation rejects both sub-cent precision and exact-cent total discrepancies.</summary>
    [TestMethod]
    [DataRow("50.00", "49.995")]
    [DataRow("50.00", "49.99")]
    [DataRow("50.005", "49.995")]
    public async Task Protocol_split_create_rejects_near_miss_and_subcent_lines(string first, string second)
    {
        var token = await OAuthFlow.IssueTokenAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}", login: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var result = await CallAsync("transactions_splits_create", new
        {
            input = new { accountId = ownedAccountId, date = "2026-01-15", amount = -100m },
            splits = new[]
            {
                new { amount = -decimal.Parse(first, System.Globalization.CultureInfo.InvariantCulture) },
                new { amount = -decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture) }
            },
            confirmed = true
        });

        AssertToolError(result);
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.AreEqual(2, await db.Transactions.CountAsync());
        Assert.AreEqual(0, await db.TransactionSplits.CountAsync());
    }

    private static JsonDocument ToolPayload(JsonDocument document)
    {
        var result = document.RootElement.GetProperty("result");
        Assert.IsFalse(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
        return JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString() ??
                                  throw new AssertFailedException("Tool omitted its JSON payload."));
    }

    private async Task<JsonDocument> CallAsync(string name, object arguments)
    {
        using var response = await McpHttpTests.SendAsync(client, "tools/call", new { name, arguments });
        return await McpHttpTests.ReadRpcAsync(response);
    }

    private static void AssertToolError(JsonDocument document)
    {
        var result = document.RootElement.GetProperty("result");
        Assert.IsTrue(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
    }
}
