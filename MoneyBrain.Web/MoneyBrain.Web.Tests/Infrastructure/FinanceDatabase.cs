using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MoneyBrain.Web.Application.Common.Services;
using MoneyBrain.Web.Application.Transactions;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Tests.Infrastructure;

internal sealed class FinanceDatabase : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
{
    internal const string Owner = "owner";
    internal const string Other = "other";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly MemoryCache memoryCache = new(new MemoryCacheOptions());
    private MemoryCacheService? cache;
    private DbContextOptions<ApplicationDbContext>? options;

    internal TransactionService Transactions => new(this, new LedgerService(this), Cache);
    internal TransferService Transfers => new(this, new LedgerService(this), Cache);
    internal MemoryCacheService Cache => cache ??= new(memoryCache);
    internal Account Checking { get; private set; } = null!;
    internal Account Savings { get; private set; } = null!;
    internal Account ForeignAccount { get; private set; } = null!;
    internal Category Category { get; private set; } = null!;

    internal async Task InitializeAsync()
    {
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        context.Users.AddRange(
            new ApplicationUser { Id = Owner, UserName = "owner@example.test" },
            new ApplicationUser { Id = Other, UserName = "other@example.test" });
        Checking = Account(Owner, "Checking");
        Savings = Account(Owner, "Savings");
        ForeignAccount = Account(Other, "Private account");
        var group = new CategoryGroup { UserId = Owner, Name = "Expenses", Type = CategoryType.Expense };
        Category = new Category { UserId = Owner, Name = "Food", CategoryGroup = group };
        context.AddRange(Checking, Savings, ForeignAccount, group, Category);
        await context.SaveChangesAsync();
    }

    public ApplicationDbContext CreateDbContext() => new(options ??
        throw new InvalidOperationException("Initialize the fixture before creating a context."));

    internal Task<Transaction> CreateTransactionAsync(decimal amount = -100m) =>
        Transactions.CreateTransactionAsync(Owner, Checking.Id, new DateTime(2026, 1, 15), amount,
            null, Category.Id, "Original memo", TransactionStatus.Posted, false, null, null);

    private static Account Account(string owner, string name) => new()
    {
        UserId = owner, Name = name, Type = AccountType.Asset, SubType = AccountSubType.Checking,
        OpeningBalance = 1000m
    };

    public async ValueTask DisposeAsync()
    {
        memoryCache.Dispose();
        await connection.DisposeAsync();
    }
}
