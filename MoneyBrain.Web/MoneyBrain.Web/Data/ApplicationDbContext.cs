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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);

            // Relationship: Account belongs to one ApplicationUser
            entity.HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => new { a.UserId, a.IsActive });
            entity.HasIndex(a => new { a.UserId, a.Type });

            // Decimal precision for money values
            entity.Property(a => a.OpeningBalance)
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
            entity.HasOne(us => us.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique index on UserId (one settings per user)
            entity.HasIndex(us => us.UserId).IsUnique();
        });
    }
}