using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Runs dynamic query expressions against a real SQLite provider and owned data.</summary>
[TestClass]
public sealed class InsightExplorerTests
{
    /// <summary>Date expressions must compare the model's DateTime type, not a DateTimeOffset constant.</summary>
    [TestMethod]
    public async Task Date_range_query_returns_only_owned_transactions_inside_range()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var expected = await database.CreateTransactionAsync();
        await using (var context = database.CreateDbContext())
        {
            context.Transactions.AddRange(
                new Transaction
                {
                    UserId = FinanceDatabase.Owner, AccountId = database.Checking.Id,
                    Date = new DateTime(2025, 12, 31), Amount = -30m
                },
                new Transaction
                {
                    UserId = FinanceDatabase.Other, AccountId = database.ForeignAccount.Id,
                    Date = new DateTime(2026, 1, 15), Amount = -40m
                });
            await context.SaveChangesAsync();
        }
        var service = new InsightExplorerService(database);
        var query = new QueryDefinition
        {
            Filters =
            [
                new FilterCondition
                {
                    Property = "Date", DataType = PropertyDataType.DateTime, Operator = FilterOperator.Between,
                    Value = "2026-01-01", SecondValue = "2026-01-31"
                }
            ]
        };

        var result = await service.ExecuteQueryAsync(FinanceDatabase.Owner, query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.HasCount(1, result.Rows);
        Assert.AreEqual(expected.Id, result.Rows[0]["Id"]);
    }

    /// <summary>Decimal constants are lifted to the nullable property type before expression construction.</summary>
    [TestMethod]
    public async Task Nullable_decimal_filter_matches_non_null_owned_values()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        await using (var context = database.CreateDbContext())
        {
            var account = await context.Accounts.SingleAsync(account => account.Id == database.Checking.Id);
            account.MonthlySpendingLimit = 400m;
            var foreign = await context.Accounts.SingleAsync(account => account.Id == database.ForeignAccount.Id);
            foreign.MonthlySpendingLimit = 400m;
            await context.SaveChangesAsync();
        }
        var query = new QueryDefinition
        {
            TargetEntity = QueryTargetEntity.Account,
            Filters =
            [
                new FilterCondition
                {
                    Property = "MonthlySpendingLimit", DataType = PropertyDataType.Decimal,
                    Operator = FilterOperator.Equals, Value = "400"
                }
            ]
        };

        var result = await new InsightExplorerService(database).ExecuteQueryAsync(FinanceDatabase.Owner, query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual(database.Checking.Id, result.Rows.Single()["Id"]);
    }

    /// <summary>Typed enum filters use the entity enum type and retain owner isolation.</summary>
    [TestMethod]
    public async Task Account_subtype_filter_matches_only_owned_accounts()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        await using (var context = database.CreateDbContext())
        {
            var account = await context.Accounts.SingleAsync(account => account.Id == database.Savings.Id);
            account.SubType = AccountSubType.Savings;
            await context.SaveChangesAsync();
        }
        var query = new QueryDefinition
        {
            TargetEntity = QueryTargetEntity.Account,
            Filters =
            [
                new FilterCondition
                {
                    Property = "SubType", DataType = PropertyDataType.Enum,
                    Operator = FilterOperator.Equals, Value = "Checking"
                }
            ]
        };

        var result = await new InsightExplorerService(database).ExecuteQueryAsync(FinanceDatabase.Owner, query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual(database.Checking.Id, result.Rows.Single()["Id"]);
    }

    /// <summary>Saved definitions can be loaded and executed by their owner but not by another user.</summary>
    [TestMethod]
    public async Task Saved_query_load_update_delete_are_owner_scoped()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new InsightExplorerService(database);
        var saved = await service.SaveQueryAsync(FinanceDatabase.Owner, "My query", null, new QueryDefinition());

        Assert.IsNull(await service.LoadQueryAsync(saved.Id, FinanceDatabase.Other));
        Assert.IsFalse(await service.UpdateQueryAsync(saved.Id, FinanceDatabase.Other, "Changed", null, new QueryDefinition()));
        Assert.IsFalse(await service.DeleteQueryAsync(saved.Id, FinanceDatabase.Other));
        var owned = await service.LoadQueryAsync(saved.Id, FinanceDatabase.Owner);
        Assert.IsNotNull(owned);
        Assert.AreEqual("My query", owned.Name);
    }
}
