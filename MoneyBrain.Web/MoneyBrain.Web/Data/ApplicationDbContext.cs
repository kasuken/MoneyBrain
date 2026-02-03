using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>
    /// Accounts represent asset and liability accounts (bank, cash, credit cards, loans).
    /// </summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>
    /// Opening balance adjustments provide audit trail for account opening balance changes.
    /// </summary>
    public DbSet<OpeningBalanceAdjustment> OpeningBalanceAdjustments => Set<OpeningBalanceAdjustment>();

    /// <summary>
    /// Account balance snapshots track point-in-time balance values for history and reporting.
    /// </summary>
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();

    /// <summary>
    /// Manual balance adjustments track explicit balance changes with full audit trail.
    /// </summary>
    public DbSet<ManualBalanceAdjustment> ManualBalanceAdjustments => Set<ManualBalanceAdjustment>();

    /// <summary>
    /// User settings store personal preferences like currency and timezone.
    /// </summary>
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    /// <summary>
    /// Category groups for organizing categories
    /// </summary>
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();

    /// <summary>
    /// Categories for transactions - each belongs to exactly one group
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Payees (vendors, people, etc.) for transactions
    /// </summary>
    public DbSet<Payee> Payees => Set<Payee>();

    /// <summary>
    /// Transactions - each belongs to exactly one account
    /// </summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>
    /// Transaction splits - sum must equal transaction amount
    /// </summary>
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();

    /// <summary>
    /// Saved transaction filters for quick access
    /// </summary>
    public DbSet<SavedTransactionFilter> SavedTransactionFilters => Set<SavedTransactionFilter>();

    /// <summary>
    /// Monthly budgets (envelopes) for categories - planned amounts per category per month
    /// </summary>
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();

    /// <summary>
    /// Named budgets containing multiple category allocations for a specific period
    /// </summary>
    public DbSet<Budget> Budgets => Set<Budget>();

    /// <summary>
    /// Budget category allocations - links budgets to categories with planned amounts
    /// </summary>
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();

    /// <summary>
    /// Account reconciliations track statement matching sessions
    /// </summary>
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();

    /// <summary>
    /// Ledger entries for double-entry bookkeeping - every transaction generates at least 2 entries
    /// </summary>
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Identity Passkey JSON column for PostgreSQL compatibility
        // PostgreSQL requires 'jsonb' type for JSON columns; SQLite uses 'TEXT' which is set by default
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var passkeyEntity = modelBuilder.Model.FindEntityType(typeof(IdentityUserPasskey<string>));
            var dataProperty = passkeyEntity?.FindProperty("Data");
            dataProperty?.SetColumnType("jsonb");
        }

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);

            // Relationship: Account belongs to one ApplicationUser
            entity.HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Credit card can link to payment account
            entity.HasOne(a => a.LinkedPaymentAccount)
                .WithMany()
                .HasForeignKey(a => a.LinkedPaymentAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for common queries
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => new { a.UserId, a.IsActive });
            entity.HasIndex(a => new { a.UserId, a.Type });
            entity.HasIndex(a => a.BillingCycleDay);

            // Decimal precision for money values
            entity.Property(a => a.OpeningBalance)
                .HasPrecision(18, 2);

            entity.Property(a => a.MonthlySpendingLimit)
                .HasPrecision(18, 2);
        });

        // Configure OpeningBalanceAdjustment entity
        modelBuilder.Entity<OpeningBalanceAdjustment>(entity =>
        {
            entity.HasKey(oba => oba.Id);

            // Relationship: Adjustment belongs to one Account
            entity.HasOne(oba => oba.Account)
                .WithMany(a => a.OpeningBalanceAdjustments)
                .HasForeignKey(oba => oba.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(oba => oba.AccountId);
            entity.HasIndex(oba => oba.AdjustedAt);
            entity.HasIndex(oba => new { oba.AccountId, oba.AdjustedAt });

            // Decimal precision for money values
            entity.Property(oba => oba.PreviousBalance).HasPrecision(18, 2);
            entity.Property(oba => oba.NewBalance).HasPrecision(18, 2);
            entity.Property(oba => oba.AdjustmentAmount).HasPrecision(18, 2);
        });

        // Configure AccountBalanceSnapshot entity
        modelBuilder.Entity<AccountBalanceSnapshot>(entity =>
        {
            entity.HasKey(abs => abs.Id);

            // Relationship: Snapshot belongs to one Account
            entity.HasOne(abs => abs.Account)
                .WithMany(a => a.BalanceSnapshots)
                .HasForeignKey(abs => abs.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(abs => abs.AccountId);
            entity.HasIndex(abs => abs.SnapshotDate);
            entity.HasIndex(abs => new { abs.AccountId, abs.SnapshotDate });
            entity.HasIndex(abs => new { abs.AccountId, abs.Type });

            // Decimal precision for money values
            entity.Property(abs => abs.Balance).HasPrecision(18, 2);
        });

        // Configure ManualBalanceAdjustment entity
        modelBuilder.Entity<ManualBalanceAdjustment>(entity =>
        {
            entity.HasKey(mba => mba.Id);

            // Relationship: Adjustment belongs to one Account
            entity.HasOne(mba => mba.Account)
                .WithMany(a => a.ManualBalanceAdjustments)
                .HasForeignKey(mba => mba.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(mba => mba.AccountId);
            entity.HasIndex(mba => mba.AdjustmentDate);
            entity.HasIndex(mba => new { mba.AccountId, mba.AdjustmentDate });
            entity.HasIndex(mba => new { mba.AccountId, mba.IsReconciled });
            entity.HasIndex(mba => mba.Category);

            // Decimal precision for money values
            entity.Property(mba => mba.Amount).HasPrecision(18, 2);
        });

        // Configure UserSettings entity
        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(us => us.Id);

            // Relationship: One user has one settings record
            // Make the relationship optional to avoid FK constraint issues during setup
            entity.HasOne(us => us.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Unique index on UserId (one settings per user)
            entity.HasIndex(us => us.UserId).IsUnique();
        });

        // Configure CategoryGroup entity
        modelBuilder.Entity<CategoryGroup>(entity =>
        {
            entity.HasKey(cg => cg.Id);

            entity.HasIndex(cg => cg.UserId);
            entity.HasIndex(cg => new { cg.UserId, cg.IsActive });
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);

            // Relationship: Category belongs to one CategoryGroup
            entity.HasOne(c => c.CategoryGroup)
                .WithMany(cg => cg.Categories)
                .HasForeignKey(c => c.CategoryGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.CategoryGroupId);
            entity.HasIndex(c => new { c.UserId, c.IsActive });
        });

        // Configure Payee entity
        modelBuilder.Entity<Payee>(entity =>
        {
            entity.HasKey(p => p.Id);

            // Relationship: Payee can have a default Category
            entity.HasOne(p => p.DefaultCategory)
                .WithMany()
                .HasForeignKey(p => p.DefaultCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => new { p.UserId, p.IsActive });
        });

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);

            // Relationship: Transaction belongs to one Account
            entity.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Transaction can have a Payee
            entity.HasOne(t => t.Payee)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PayeeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Transaction can have a Category
            entity.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Transaction can link to another transaction (transfer)
            entity.HasOne(t => t.TransferTransaction)
                .WithMany()
                .HasForeignKey(t => t.TransferTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Credit card bill links to source credit card account
            entity.HasOne(t => t.CreditCardBillingSourceAccount)
                .WithMany()
                .HasForeignKey(t => t.CreditCardBillingSourceAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for common queries
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.AccountId);
            entity.HasIndex(t => t.Date);
            entity.HasIndex(t => new { t.UserId, t.Date });
            entity.HasIndex(t => new { t.AccountId, t.Date });
            entity.HasIndex(t => new { t.UserId, t.Status });
            entity.HasIndex(t => new { t.UserId, t.IsReconciled });
            entity.HasIndex(t => t.PayeeId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.CreditCardBillingSourceAccountId);

            // Decimal precision for money values
            entity.Property(t => t.Amount).HasPrecision(18, 2);
        });

        // Configure TransactionSplit entity
        modelBuilder.Entity<TransactionSplit>(entity =>
        {
            entity.HasKey(ts => ts.Id);

            // Relationship: Split belongs to one Transaction
            entity.HasOne(ts => ts.Transaction)
                .WithMany(t => t.Splits)
                .HasForeignKey(ts => ts.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Split can have a Category
            entity.HasOne(ts => ts.Category)
                .WithMany()
                .HasForeignKey(ts => ts.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(ts => ts.TransactionId);
            entity.HasIndex(ts => ts.CategoryId);

            // Decimal precision for money values
            entity.Property(ts => ts.Amount).HasPrecision(18, 2);
        });

        // Configure SavedTransactionFilter entity
        modelBuilder.Entity<SavedTransactionFilter>(entity =>
        {
            entity.HasKey(stf => stf.Id);

            entity.HasIndex(stf => stf.UserId);
            entity.HasIndex(stf => new { stf.UserId, stf.IsDefault });

            entity.Property(stf => stf.Name).IsRequired().HasMaxLength(100);
            entity.Property(stf => stf.FilterJson).IsRequired();
        });

        // Configure MonthlyBudget entity
        modelBuilder.Entity<MonthlyBudget>(entity =>
        {
            entity.HasKey(mb => mb.Id);

            // Relationship: Budget belongs to one Category
            entity.HasOne(mb => mb.Category)
                .WithMany()
                .HasForeignKey(mb => mb.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(mb => mb.UserId);
            entity.HasIndex(mb => mb.CategoryId);
            entity.HasIndex(mb => new { mb.CategoryId, mb.IsDefault });
            entity.HasIndex(mb => new { mb.UserId, mb.Year, mb.Month });
            entity.HasIndex(mb => new { mb.CategoryId, mb.Year, mb.Month });

            // Unique constraint: one default budget per category
            entity.HasIndex(mb => new { mb.CategoryId, mb.IsDefault })
                .IsUnique()
                .HasFilter("[IsDefault] = 1");

            // Unique constraint: one budget per category per month (for non-default budgets)
            entity.HasIndex(mb => new { mb.CategoryId, mb.Year, mb.Month })
                .IsUnique()
                .HasFilter("[IsDefault] = 0");

            // Decimal precision for money values
            entity.Property(mb => mb.PlannedAmount).HasPrecision(18, 2);
        });

        // Configure Budget entity
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Description).HasMaxLength(500);
            entity.Property(b => b.UserId).IsRequired();

            // Indexes for common queries
            entity.HasIndex(b => b.UserId);
            entity.HasIndex(b => new { b.UserId, b.Year, b.Month });
            entity.HasIndex(b => new { b.UserId, b.Name });
            
            // Unique constraint: one default budget with a given name per user
            entity.HasIndex(b => new { b.UserId, b.Name, b.IsDefault })
                .IsUnique()
                .HasFilter("[IsDefault] = 1");
            
            // Unique constraint: one budget with a given name per period (for non-defaults)
            entity.HasIndex(b => new { b.UserId, b.Name, b.Year, b.Month })
                .IsUnique()
                .HasFilter("[IsDefault] = 0");

            // Relationship: Budget has many BudgetCategories
            entity.HasMany(b => b.BudgetCategories)
                .WithOne(bc => bc.Budget)
                .HasForeignKey(bc => bc.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BudgetCategory entity
        modelBuilder.Entity<BudgetCategory>(entity =>
        {
            entity.HasKey(bc => bc.Id);

            entity.Property(bc => bc.PlannedAmount).HasPrecision(18, 2);

            // Unique constraint: one entry per budget per category
            entity.HasIndex(bc => new { bc.BudgetId, bc.CategoryId }).IsUnique();

            // Relationship: BudgetCategory belongs to one Budget
            entity.HasOne(bc => bc.Budget)
                .WithMany(b => b.BudgetCategories)
                .HasForeignKey(bc => bc.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: BudgetCategory references one Category
            entity.HasOne(bc => bc.Category)
                .WithMany()
                .HasForeignKey(bc => bc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure LedgerEntry entity
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(le => le.Id);

            // Relationship: LedgerEntry belongs to one Transaction
            entity.HasOne(le => le.Transaction)
                .WithMany(t => t.LedgerEntries)
                .HasForeignKey(le => le.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: LedgerEntry affects one Account
            entity.HasOne(le => le.Account)
                .WithMany()
                .HasForeignKey(le => le.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship: LedgerEntry can be associated with a Category
            entity.HasOne(le => le.Category)
                .WithMany()
                .HasForeignKey(le => le.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for common queries
            entity.HasIndex(le => le.UserId);
            entity.HasIndex(le => le.TransactionId);
            entity.HasIndex(le => le.AccountId);
            entity.HasIndex(le => le.EntryDate);
            entity.HasIndex(le => new { le.AccountId, le.EntryDate });
            entity.HasIndex(le => new { le.UserId, le.EntryDate });

            // Decimal precision for money values
            entity.Property(le => le.DebitAmount).HasPrecision(18, 2);
            entity.Property(le => le.CreditAmount).HasPrecision(18, 2);
        });
    }
}